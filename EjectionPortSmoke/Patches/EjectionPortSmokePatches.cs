using System;
using System.Collections.Generic;
using System.Reflection;
using EFT;
using EFT.AssetsManager;
using SPT.Reflection.Patching;
using UnityEngine;

namespace EjectionPortSmoke;

internal sealed class FirearmsShellExtractionPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return typeof(Firearms).GetMethod(
                   nameof(Firearms.UseGameObjectForShellExtraction),
                   BindingFlags.Instance | BindingFlags.Public,
                   null,
                   new[] { typeof(Vector3), typeof(Vector3), typeof(AmmoPoolObject), typeof(Vector3) },
                   null)
               ?? throw new MissingMethodException(typeof(Firearms).FullName,
                   nameof(Firearms.UseGameObjectForShellExtraction));
    }

    [PatchPostfix]
    private static void Postfix(Firearms __instance, Vector3 force, AmmoPoolObject shell, Vector3 parentForce)
    {
        Transform shellPort = FindClosestShellPort(__instance._shellPortsTransforms, shell);
        EjectionPortSmokeEmitter.Emit(shell, force, parentForce, false, shellPort);
    }

    private static Transform FindClosestShellPort(IReadOnlyList<Transform> shellPorts, AmmoPoolObject shell)
    {
        if (shellPorts == null || shellPorts.Count == 0 || shell == null)
        {
            return null;
        }

        Vector3 shellPosition = shell.transform.position;
        Transform closest = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < shellPorts.Count; i++)
        {
            Transform candidate = shellPorts[i];
            if (candidate == null)
            {
                continue;
            }

            float distance = (candidate.position - shellPosition).sqrMagnitude;
            if (distance < closestDistance)
            {
                closest = candidate;
                closestDistance = distance;
            }
        }

        return closest;
    }
}

internal sealed class UnderbarrelShellExtractionPatch : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        Type underbarrelType = typeof(Player.FirearmController.UnderbarrelContainer);
        return underbarrelType.GetMethod(
                   nameof(Player.FirearmController.UnderbarrelContainer.UseGameObjectForShellExtraction),
                   BindingFlags.Instance | BindingFlags.Public,
                   null,
                   new[] { typeof(Vector3), typeof(Vector3), typeof(AmmoPoolObject), typeof(Vector3) },
                   null)
               ?? throw new MissingMethodException(underbarrelType.FullName,
                   nameof(Player.FirearmController.UnderbarrelContainer.UseGameObjectForShellExtraction));
    }

    [PatchPostfix]
    private static void Postfix(Player.FirearmController.UnderbarrelContainer __instance, Vector3 force,
        AmmoPoolObject shell, Vector3 parentForce)
    {
        EjectionPortSmokeEmitter.Emit(shell, force, parentForce, true, __instance._shellPortTransform);
    }
}
