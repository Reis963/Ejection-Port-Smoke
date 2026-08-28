using System;
using System.Collections;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT.AssetsManager;
using Systems.Effects;
using UnityEngine;

namespace EjectionPortSmoke;

internal static class EjectionPortSmokeEmitter
{
    private const int MaximumParticleCount = 32;

    private static readonly object RandomLock = new();
    private static readonly System.Random Random = new(Environment.TickCount);

    private static ConfigEntry<bool> _enabled;
    private static ConfigEntry<bool> _underbarrelEnabled;
    private static ConfigEntry<int> _particleCount;
    private static ConfigEntry<float> _emissionDuration;
    private static ConfigEntry<float> _speed;
    private static ConfigEntry<float> _coneAngle;
    private static ConfigEntry<float> _size;
    private static ConfigEntry<float> _lifetime;
    private static ConfigEntry<float> _positionSpread;
    private static ConfigEntry<float> _spreadSpeed;
    private static ConfigEntry<float> _riseSpeed;
    private static ConfigEntry<float> _ejectionVelocityInheritance;
    private static ConfigEntry<float> _opacity;
    private static ConfigEntry<bool> _debugLogging;
    private static ManualLogSource _logger;
    private static MonoBehaviour _coroutineHost;
    private static bool _emissionErrorLogged;

    internal static void Bind(ConfigFile config, ManualLogSource logger, MonoBehaviour coroutineHost)
    {
        _logger = logger;
        _coroutineHost = coroutineHost;

        _enabled = config.Bind("General", "Enabled", true,
            "Emit smoke whenever Tarkov physically ejects a cartridge or shell.");
        _underbarrelEnabled = config.Bind("General", "IncludeUnderbarrel", true,
            "Also emit smoke from underbarrel weapon cartridge ejections.");
        _debugLogging = config.Bind("General", "DebugLogging", false,
            "Log each scheduled and completed smoke emission. Intended only for troubleshooting.");

        _particleCount = config.Bind("Smoke", "ParticleCount", 12,
            "Number of particles emitted over the emission duration (2-32).");
        _emissionDuration = config.Bind("Smoke", "EmissionDuration", 0.18f,
            "How long the smoke source follows the weapon's ejection port in seconds (0.03-0.6).");
        _speed = config.Bind("Smoke", "Speed", 1.5f,
            "Base outward speed of smoke particles in world units per second (0-3.0).");
        _coneAngle = config.Bind("Smoke", "ConeAngle", 18f,
            "Half-angle of the emission cone in degrees (0-60).");
        _size = config.Bind("Smoke", "Size", 0.12f,
            "Base particle size in world units (0.01-1.0).");
        _lifetime = config.Bind("Smoke", "Lifetime", 0.25f,
            "Base particle lifetime in seconds (0.1-3.0).");
        _positionSpread = config.Bind("Smoke", "PositionSpread", 0.015f,
            "Random starting-position radius around the active ejection port (0-0.5).");
        _spreadSpeed = config.Bind("Smoke", "SpreadSpeed", 0.12f,
            "Random expansion speed applied to the smoke (0-2.0).");
        _riseSpeed = config.Bind("Smoke", "RiseSpeed", 0.16f,
            "Upward velocity applied to the smoke (0-2.0).");
        _ejectionVelocityInheritance = config.Bind("Smoke", "EjectionVelocityInheritance", 0.06f,
            "Fraction of the shell's initial world velocity inherited by the smoke (0-1).");
        _opacity = config.Bind("Smoke", "Opacity", 0.42f,
            "Starting alpha of the smoke particles (0-1).");
    }

    internal static void Emit(AmmoPoolObject shell, Vector3 localEjectionForce, Vector3 parentVelocity,
        bool underbarrel, Transform emissionAnchor)
    {
        if (_emissionErrorLogged)
        {
            return;
        }

        try
        {
            EmitCore(shell, localEjectionForce, parentVelocity, underbarrel, emissionAnchor);
        }
        catch (Exception exception)
        {
            DisableAfterError(exception);
        }
    }

    private static void EmitCore(AmmoPoolObject shell, Vector3 localEjectionForce, Vector3 parentVelocity,
        bool underbarrel, Transform emissionAnchor)
    {
        if (_enabled?.Value != true || shell == null || (underbarrel && _underbarrelEnabled?.Value != true))
        {
            return;
        }

        ParticleSystem fume = GetFume();
        if (fume == null)
        {
            return;
        }

        Transform shellTransform = shell.transform;
        Vector3 worldEjectionVelocity = shellTransform.rotation * localEjectionForce + parentVelocity;
        var snapshot = new EmissionSnapshot(
            shellTransform.position,
            worldEjectionVelocity,
            Mathf.Clamp(_size.Value, 0.01f, 1f),
            Mathf.Clamp(_lifetime.Value, 0.1f, 3f),
            Mathf.Clamp(_positionSpread.Value, 0f, 0.5f),
            Mathf.Clamp(_spreadSpeed.Value, 0f, 2f),
            Mathf.Clamp(_riseSpeed.Value, 0f, 2f),
            Mathf.Clamp01(_ejectionVelocityInheritance.Value),
            Mathf.Clamp01(_opacity.Value));
        var settings = new EmissionSettings(
            Mathf.Clamp(_particleCount.Value, 2, MaximumParticleCount),
            Mathf.Clamp(_emissionDuration.Value, 0.03f, 0.6f),
            Mathf.Clamp(_speed.Value, 0f, 3f),
            Mathf.Clamp(_coneAngle.Value, 0f, 60f));

        Vector3 firstOrigin;
        EmitParticle(fume, snapshot, emissionAnchor, settings, 0, out firstOrigin);
        ScheduleRemainingParticles(snapshot, emissionAnchor, settings);

        if (_debugLogging.Value)
        {
            string anchor = emissionAnchor != null ? emissionAnchor.name : "fixed fallback";
            _logger.LogInfo($"Scheduled {settings.ParticleCount} ejection-port smoke particles at {firstOrigin} "
                            + $"(underbarrel: {underbarrel}, anchor: {anchor})");
        }
    }

    private static void ScheduleRemainingParticles(EmissionSnapshot snapshot, Transform emissionAnchor,
        EmissionSettings settings)
    {
        if (_coroutineHost == null)
        {
            ParticleSystem fume = GetFume();
            if (fume == null)
            {
                return;
            }

            for (int i = 1; i < settings.ParticleCount; i++)
            {
                EmitParticle(fume, snapshot, emissionAnchor, settings, i, out _);
            }

            return;
        }

        _coroutineHost.StartCoroutine(EmitOverTime(snapshot, emissionAnchor, settings));
    }

    private static IEnumerator EmitOverTime(EmissionSnapshot snapshot, Transform emissionAnchor,
        EmissionSettings settings)
    {
        float startTime = Time.time;
        float interval = settings.Duration / (settings.ParticleCount - 1);

        for (int i = 1; i < settings.ParticleCount; i++)
        {
            float targetTime = startTime + interval * i;
            while (Time.time < targetTime)
            {
                yield return null;
            }

            if (!TryEmitParticle(snapshot, emissionAnchor, settings, i))
            {
                yield break;
            }
        }

        if (_debugLogging.Value)
        {
            _logger.LogInfo($"Completed emission of {settings.ParticleCount} ejection-port smoke particles over "
                            + $"{settings.Duration:F3}s (moving anchor: {emissionAnchor != null})");
        }
    }

    private static bool TryEmitParticle(EmissionSnapshot snapshot, Transform emissionAnchor,
        EmissionSettings settings, int index)
    {
        if (_enabled?.Value != true || _emissionErrorLogged)
        {
            return false;
        }

        try
        {
            ParticleSystem fume = GetFume();
            if (fume == null)
            {
                return false;
            }

            EmitParticle(fume, snapshot, emissionAnchor, settings, index, out _);
            return true;
        }
        catch (Exception exception)
        {
            DisableAfterError(exception);
            return false;
        }
    }

    private static void EmitParticle(ParticleSystem fume, EmissionSnapshot snapshot, Transform emissionAnchor,
        EmissionSettings settings, int index, out Vector3 origin)
    {
        float progress = index / (float)(settings.ParticleCount - 1);
        origin = emissionAnchor != null ? emissionAnchor.position : snapshot.FallbackOrigin;

        Vector3 axis = snapshot.WorldEjectionVelocity.sqrMagnitude > 0.000001f
            ? snapshot.WorldEjectionVelocity.normalized
            : emissionAnchor != null ? emissionAnchor.forward : Vector3.up;
        Vector3 direction = NextDirectionInCone(axis, settings.ConeAngle);
        Vector3 randomDirection = NextUnitVector();
        float positionRadius = snapshot.PositionSpread * 0.35f * NextFloat();
        float sizeVariation = Mathf.Lerp(0.85f, 1.15f, NextFloat());
        float lifetimeVariation = Mathf.Lerp(0.9f, 1.1f, NextFloat());
        float speedVariation = Mathf.Lerp(0.8f, 1.2f, NextFloat());
        float opacityMultiplier = Mathf.Lerp(0.70f, 0.35f, progress);
        byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(snapshot.Opacity * opacityMultiplier) * byte.MaxValue);
        byte gray = (byte)Mathf.RoundToInt(Mathf.Lerp(215f, 190f, progress));

        var emitParams = new ParticleSystem.EmitParams
        {
            position = origin + randomDirection * positionRadius,
            velocity = direction * settings.Speed * speedVariation
                       + snapshot.WorldEjectionVelocity * snapshot.VelocityInheritance * 0.15f
                       + Vector3.up * snapshot.RiseSpeed * 0.35f
                       + randomDirection * snapshot.SpreadSpeed * 0.18f,
            startSize = snapshot.Size * Mathf.Lerp(0.38f, 0.65f, progress) * sizeVariation,
            startLifetime = snapshot.Lifetime * Mathf.Lerp(0.75f, 1.35f, progress) * lifetimeVariation,
            startColor = new Color32(gray, gray, gray, alpha),
            rotation = NextFloat() * 360f,
            randomSeed = NextSeed()
        };

        fume.Emit(emitParams, 1);
    }

    private static ParticleSystem GetFume()
    {
        return Singleton<Effects>.Instantiated ? Singleton<Effects>.Instance?.MuzzleEffect.Fume : null;
    }

    private static void DisableAfterError(Exception exception)
    {
        if (_emissionErrorLogged)
        {
            return;
        }

        _emissionErrorLogged = true;
        _logger?.LogError($"Ejection-port smoke emission failed and has been disabled: {exception}");
    }

    private static Vector3 NextDirectionInCone(Vector3 axis, float halfAngleDegrees)
    {
        if (halfAngleDegrees <= 0.001f)
        {
            return axis.normalized;
        }

        float minimumCosine = Mathf.Cos(halfAngleDegrees * Mathf.Deg2Rad);
        float cosine = Mathf.Lerp(minimumCosine, 1f, NextFloat());
        float sine = Mathf.Sqrt(Mathf.Max(0f, 1f - cosine * cosine));
        float angle = NextFloat() * Mathf.PI * 2f;
        var localDirection = new Vector3(sine * Mathf.Cos(angle), sine * Mathf.Sin(angle), cosine);
        return Quaternion.FromToRotation(Vector3.forward, axis.normalized) * localDirection;
    }

    private static Vector3 NextUnitVector()
    {
        float z = NextFloat() * 2f - 1f;
        float angle = NextFloat() * Mathf.PI * 2f;
        float radius = Mathf.Sqrt(Mathf.Max(0f, 1f - z * z));
        return new Vector3(radius * Mathf.Cos(angle), z, radius * Mathf.Sin(angle));
    }

    private static float NextFloat()
    {
        lock (RandomLock)
        {
            return (float)Random.NextDouble();
        }
    }

    private static uint NextSeed()
    {
        lock (RandomLock)
        {
            var bytes = new byte[4];
            Random.NextBytes(bytes);
            return BitConverter.ToUInt32(bytes, 0);
        }
    }

    private readonly struct EmissionSnapshot
    {
        internal EmissionSnapshot(Vector3 fallbackOrigin, Vector3 worldEjectionVelocity, float size, float lifetime,
            float positionSpread, float spreadSpeed, float riseSpeed, float velocityInheritance, float opacity)
        {
            FallbackOrigin = fallbackOrigin;
            WorldEjectionVelocity = worldEjectionVelocity;
            Size = size;
            Lifetime = lifetime;
            PositionSpread = positionSpread;
            SpreadSpeed = spreadSpeed;
            RiseSpeed = riseSpeed;
            VelocityInheritance = velocityInheritance;
            Opacity = opacity;
        }

        internal Vector3 FallbackOrigin { get; }
        internal Vector3 WorldEjectionVelocity { get; }
        internal float Size { get; }
        internal float Lifetime { get; }
        internal float PositionSpread { get; }
        internal float SpreadSpeed { get; }
        internal float RiseSpeed { get; }
        internal float VelocityInheritance { get; }
        internal float Opacity { get; }
    }

    private readonly struct EmissionSettings
    {
        internal EmissionSettings(int particleCount, float duration, float speed, float coneAngle)
        {
            ParticleCount = particleCount;
            Duration = duration;
            Speed = speed;
            ConeAngle = coneAngle;
        }

        internal int ParticleCount { get; }
        internal float Duration { get; }
        internal float Speed { get; }
        internal float ConeAngle { get; }
    }
}
