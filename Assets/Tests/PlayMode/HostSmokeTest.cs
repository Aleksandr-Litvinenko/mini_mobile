using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// Boots the real Main scene, starts a host and verifies that the networked world
/// (player hero, towers, bases, game manager) actually spawns.
public class HostSmokeTest
{
    [UnityTest]
    public IEnumerator HostStartsAndWorldSpawns()
    {
        SceneManager.LoadScene("Main", LoadSceneMode.Single);
        yield return null;
        yield return null;

        var nm = NetworkManager.Singleton;
        Assert.IsNotNull(nm, "NetworkManager not found in Main scene");
        Assert.IsNotNull(nm.NetworkConfig.PlayerPrefab, "PlayerPrefab not assigned");

        Assert.IsTrue(nm.StartHost(), "StartHost failed");
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < deadline &&
               (!nm.IsListening || nm.SpawnManager == null ||
                nm.SpawnManager.SpawnedObjects.Count < 6))
            yield return null;

        Assert.IsTrue(nm.IsHost, "Not a host after StartHost");

        // expected: GameManager + 2 towers + 2 bases + host hero = 6 networked objects
        int spawned = nm.SpawnManager.SpawnedObjects.Count;
        Assert.GreaterOrEqual(spawned, 6, "Expected at least 6 spawned network objects, got " + spawned);

        var player = nm.LocalClient.PlayerObject;
        Assert.IsNotNull(player, "Host player object (hero) was not spawned");

        // every networked object must have a valid prefab hash (catches GlobalObjectIdHash=0)
        foreach (var kv in nm.SpawnManager.SpawnedObjects)
            Assert.AreNotEqual(0u, kv.Value.PrefabIdHash,
                "NetworkObject with zero hash: " + kv.Value.name);

        // hero should sit at its team spawn (placement RPC/teleport worked)
        yield return new WaitForSecondsRealtime(1f);
        Assert.Less(player.transform.position.x, -20f,
            "Host hero was not teleported to the blue spawn, x=" + player.transform.position.x);

        nm.Shutdown();
        yield return new WaitForSecondsRealtime(1f);
    }

    [UnityTest]
    public IEnumerator MinionWavesSpawnAndPushTheLane()
    {
        SceneManager.LoadScene("Main", LoadSceneMode.Single);
        yield return null;
        yield return null;

        var nm = NetworkManager.Singleton;
        Assert.IsTrue(nm.StartHost(), "StartHost failed");
        yield return new WaitForSecondsRealtime(1f);

        // force-start the match (normally needs a second player)
        var gmType = System.Type.GetType("Moba.GameManager, Assembly-CSharp");
        Assert.IsNotNull(gmType, "Moba.GameManager type not found");
        var gm = Object.FindFirstObjectByType(gmType) as MonoBehaviour;
        Assert.IsNotNull(gm, "GameManager instance not found");
        var started = (NetworkVariable<bool>)gmType.GetField("GameStarted").GetValue(gm);
        started.Value = true;

        yield return new WaitForSecondsRealtime(2f);

        var minions = Object.FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        int count = 0;
        NetworkObject blueMinion = null;
        foreach (var no in minions)
            if (no.name.StartsWith("Minion"))
            {
                count++;
                if (no.transform.position.x < 0f) blueMinion = no;
            }
        Assert.GreaterOrEqual(count, 8, "Expected 8 minions (4 per team), got " + count);
        Assert.IsNotNull(blueMinion, "No blue-side minion found");

        // minions must push towards the enemy base
        float xBefore = blueMinion.transform.position.x;
        yield return new WaitForSecondsRealtime(2f);
        Assert.Greater(blueMinion.transform.position.x, xBefore + 2f,
            "Blue minion did not advance down the lane");

        nm.Shutdown();
        yield return new WaitForSecondsRealtime(1f);
    }
}
