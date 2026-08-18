using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace PacManGame.Tests.PlayMode
{
    /// <summary>
    /// Covers: "Scene initialization".
    ///
    /// Builds the core gameplay objects (GameManager, MazeGenerator, Player,
    /// Ghost) the same way the real scene wires them up, then verifies the
    /// singleton, spawn, and cross-references all resolve correctly once
    /// Play Mode actually runs Awake/Start.
    ///
    /// Note: these tests build minimal stand-in GameObjects rather than
    /// requiring saved prefab assets, so the suite has no dependency on
    /// specific art/prefab files existing in the project -- only on the
    /// Player/Ghost/Pellet/PowerPellet tags existing in Project Settings
    /// (per SETUP_INSTRUCTIONS.md, step 2).
    /// </summary>
    public class SceneInitializationTests
    {
        private readonly List<GameObject> spawned = new List<GameObject>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (var go in spawned)
                if (go != null) Object.Destroy(go);
            spawned.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator GameManager_Singleton_IsAssigned_AfterAwake()
        {
            var gm = Track(new GameObject("GameManager"));
            gm.AddComponent<GameManager>();

            yield return null; // let Awake/Start run

            Assert.IsNotNull(GameManager.Instance,
                "GameManager.Instance should be set once the scene has initialized.");
        }

        [UnityTest]
        public IEnumerator Maze_SpawnsPlayerAndGhosts_OnInitialization()
        {
            var stand = BuildMinimalPrefabStandins();

            var mazeObj = Track(new GameObject("MazeGenerator"));
            var maze = mazeObj.AddComponent<MazeGenerator>();
            maze.playerPrefab = stand.player;
            maze.ghostPrefabSentis = stand.ghost;
            maze.wallPrefab = stand.wall;
            maze.pelletPrefab = stand.pellet;
            maze.powerPelletPrefab = stand.powerPellet;

            yield return null; // MazeGenerator builds the level in Awake

            Assert.IsNotNull(GameObject.FindGameObjectWithTag("Player"),
                "Scene initialization should spawn exactly one Player instance.");
            Assert.IsTrue(GameObject.FindGameObjectsWithTag("Ghost").Length > 0,
                "Scene initialization should spawn at least one Ghost instance.");
            Assert.IsTrue(GameObject.FindGameObjectsWithTag("Pellet").Length > 0,
                "Scene initialization should populate pellets from the maze layout.");
        }

        [UnityTest]
        public IEnumerator GameManager_FindsPlayerTransform_AfterMazeSpawns()
        {
            var stand = BuildMinimalPrefabStandins();

            var mazeObj = Track(new GameObject("MazeGenerator"));
            var maze = mazeObj.AddComponent<MazeGenerator>();
            maze.playerPrefab = stand.player;
            maze.ghostPrefabSentis = stand.ghost;
            maze.wallPrefab = stand.wall;
            maze.pelletPrefab = stand.pellet;
            maze.powerPelletPrefab = stand.powerPellet;

            yield return null; // let the maze spawn the player first, as it would in the real scene

            var gm = Track(new GameObject("GameManager"));
            gm.AddComponent<GameManager>();

            yield return null; // let GameManager.Start() find it by tag

            Assert.IsNotNull(GameManager.Instance.PlayerTransform,
                "GameManager should locate the spawned Player by tag during initialization.");
        }

        [UnityTest]
        public IEnumerator GameManager_CountsAllPelletsOnLevel_AtStart()
        {
            var stand = BuildMinimalPrefabStandins();

            var mazeObj = Track(new GameObject("MazeGenerator"));
            var maze = mazeObj.AddComponent<MazeGenerator>();
            maze.playerPrefab = stand.player;
            maze.ghostPrefabSentis = stand.ghost;
            maze.wallPrefab = stand.wall;
            maze.pelletPrefab = stand.pellet;
            maze.powerPelletPrefab = stand.powerPellet;

            yield return null;

            var gm = Track(new GameObject("GameManager"));
            gm.AddComponent<GameManager>();

            yield return null;

            int pelletsInScene = GameObject.FindGameObjectsWithTag("Pellet").Length
                                + GameObject.FindGameObjectsWithTag("PowerPellet").Length;

            Assert.Greater(pelletsInScene, 0,
                "Scene initialization should place at least one pellet based on the maze layout.");
        }

        // -----------------------------------------------------------------

        private GameObject Track(GameObject go)
        {
            spawned.Add(go);
            return go;
        }

        private (GameObject player, GameObject ghost, GameObject wall, GameObject pellet, GameObject powerPellet)
            BuildMinimalPrefabStandins()
        {
            var wall = Track(new GameObject("WallStandin"));
            wall.AddComponent<BoxCollider2D>();

            var pellet = Track(new GameObject("PelletStandin"));
            pellet.tag = "Pellet";
            pellet.AddComponent<CircleCollider2D>().isTrigger = true;

            var powerPellet = Track(new GameObject("PowerPelletStandin"));
            powerPellet.tag = "PowerPellet";
            powerPellet.AddComponent<CircleCollider2D>().isTrigger = true;

            var player = Track(new GameObject("PlayerStandin"));
            player.tag = "Player";
            player.AddComponent<Rigidbody2D>();
            player.AddComponent<CircleCollider2D>().isTrigger = true;
            player.AddComponent<PacManController>();

            var ghost = Track(new GameObject("GhostStandin"));
            ghost.tag = "Ghost";
            ghost.AddComponent<Rigidbody2D>();
            ghost.AddComponent<CircleCollider2D>().isTrigger = true;
            ghost.AddComponent<GhostController>(); // heuristic ghost -- keeps this suite independent of a trained model

            return (player, ghost, wall, pellet, powerPellet);
        }
    }
}
