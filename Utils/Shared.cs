using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace NPCFinder.Utils;
[HarmonyPatch]
public static class AdminSyncing
{
    private static bool _isServer;

    private static void AdminStatusSync(ZNet __instance)
    {
        _isServer = __instance.IsServer();
        ZRoutedRpc.instance.Register<ZPackage>(NpcFinderPlugin.ModName + " AdminStatusSync", RPC_AdminAbilityCheck);

        IEnumerator WatchAdminListChanges()
        {
            List<string> CurrentList = new(ZNet.instance.m_adminList.GetList());
            for (;;)
            {
                yield return new WaitForSeconds(30);
                if (!ZNet.instance.m_adminList.GetList().SequenceEqual(CurrentList))
                {
                    CurrentList = new List<string>(ZNet.instance.m_adminList.GetList());
                    List<ZNetPeer> adminPeer = ZNet.instance.GetPeers().Where(p =>
                        ZNet.instance.m_adminList.Contains(p.m_rpc.GetSocket().GetHostName())).ToList();
                    List<ZNetPeer> nonAdminPeer = ZNet.instance.GetPeers().Except(adminPeer).ToList();
                    SendAdmin(nonAdminPeer, false);
                    SendAdmin(adminPeer, true);

                    void SendAdmin(List<ZNetPeer> peers, bool isAdmin)
                    {
                        ZPackage package = new();
                        package.Write(isAdmin);
                        ZNet.instance.StartCoroutine(SendZPackage(peers, package));
                    }
                }
            }
            // ReSharper disable once IteratorNeverReturns
        }

        if (_isServer)
        {
            ZNet.instance.StartCoroutine(WatchAdminListChanges());
        }
    }

    private static IEnumerator SendZPackage(List<ZNetPeer> peers, ZPackage package)
    {
        if (!ZNet.instance)
        {
            yield break;
        }

        const int compressMinSize = 10000;

        if (package.GetArray() is { LongLength: > compressMinSize } rawData)
        {
            ZPackage compressedPackage = new();
            compressedPackage.Write(4);
            MemoryStream output = new();
            using (DeflateStream deflateStream = new(output, System.IO.Compression.CompressionLevel.Optimal))
            {
                deflateStream.Write(rawData, 0, rawData.Length);
            }

            compressedPackage.Write(output.ToArray());
            package = compressedPackage;
        }

        List<IEnumerator<bool>> writers =
            peers.Where(peer => peer.IsReady()).Select(p => TellPeerAdminStatus(p, package)).ToList();
        writers.RemoveAll(writer => !writer.MoveNext());
        while (writers.Count > 0)
        {
            yield return null;
            writers.RemoveAll(writer => !writer.MoveNext());
        }
    }

    private static IEnumerator<bool> TellPeerAdminStatus(ZNetPeer peer, ZPackage package)
    {
        if (ZRoutedRpc.instance is not { } rpc)
        {
            yield break;
        }

        SendPackage(package);

        void SendPackage(ZPackage pkg)
        {
            const string method = NpcFinderPlugin.ModName + " AdminStatusSync";
            if (_isServer)
            {
                peer.m_rpc.Invoke(method, pkg);
            }
            else
            {
                rpc.InvokeRoutedRPC(peer.m_server ? 0 : peer.m_uid, method, pkg);
            }
        }
    }

    internal static void RPC_AdminAbilityCheck(long sender, ZPackage package)
    {
        ZNetPeer? currentPeer = ZNet.instance.GetPeer(sender);
        bool admin = false;
        try
        {
            admin = package.ReadBool();
        }
        catch
        {
            // ignore
        }

        if (_isServer)
        {
            ZRoutedRpc.instance.InvokeRoutedRPC(ZRoutedRpc.Everybody,
                NpcFinderPlugin.ModName + " AdminStatusSync", new ZPackage());
            if (ZNet.instance.m_adminList.Contains(currentPeer.m_rpc.GetSocket().GetHostName()))
            {
                ZPackage pkg = new();
                pkg.Write(true);
                currentPeer.m_rpc.Invoke(NpcFinderPlugin.ModName + " AdminStatusSync", pkg);
                NpcFinderPlugin.NpcFinderLogger.LogDebug($"Admin status for {currentPeer.m_playerName} is: {admin}");
            }
        }
        else
        {
            // Remove everything they shouldn't be able to build by disabling and removing.
            NpcFinderPlugin.NpcFinderLogger.LogDebug($"Admin status: {admin}");
            NpcFinderPlugin.CanUse = admin;
        }
    }


    [HarmonyPatch(typeof(ZNet), nameof(ZNet.OnNewConnection))]
    class RegisterClientRPCPatch
    {
        private static void Postfix(ZNet __instance, ZNetPeer peer)
        {
            if (!__instance.IsServer())
            {
                peer.m_rpc.Register<ZPackage>(NpcFinderPlugin.ModName + " AdminStatusSync",
                    RPC_InitialAdminSync);
            }
            else
            {
                ZPackage packge = new();
                packge.Write(__instance.m_adminList.Contains(peer.m_rpc.GetSocket().GetHostName()));

                peer.m_rpc.Invoke(NpcFinderPlugin.ModName + " AdminStatusSync", packge);
            }
        }

        private static void RPC_InitialAdminSync(ZRpc rpc, ZPackage package) =>
            RPC_AdminAbilityCheck(0, package);
    }

    [HarmonyPatch(typeof(ZNet), nameof(ZNet.Awake))]
    [HarmonyPriority(Priority.VeryHigh)]
    static class ZNet_Awake_Patch
    {
        static void Postfix(ZNet __instance)
        {
            AdminStatusSync(__instance);
            if (!ZNet.instance.IsDedicated() && ZNet.instance.IsServer())
            {
                NpcFinderPlugin.CanUse = true;
                NpcFinderPlugin.NpcFinderLogger.LogDebug($"Local Play Detected setting Admin: {NpcFinderPlugin.CanUse}");
            }
        }
    }
}