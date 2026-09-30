using System;
using AdamsHaven.Tower;
using UnityEditor;
using UnityEngine;

public static class TowerPortValidation
{
    [MenuItem("Adams Haven/Tower/Validate Rules and Checkpoints")]
    public static void Run()
    {
        for (int slot = 1; slot <= 10; slot++)
        {
            TowerState state = TowerMilestones.Create(slot);
            var rules = new TowerRules(state);
            Require(state.day == TowerMilestones.Days[slot - 1], "Wrong day in slot " + slot);
            Require(state.slot == slot, "Wrong slot number");
            Require(state.rooms.Exists(r => r.type == "heart" && r.floor == 0 && r.x == 22), "Heart missing");
            Require(state.floors.Count == (slot == 1 ? 1 :
                new[] { 0, 3, 5, 8, 13, 19, 25, 33, 42, 49 }[slot - 1]),
                "Wrong floor count in slot " + slot);
            foreach (TowerRoom room in state.rooms)
            {
                Require(room.x >= 12 && room.x + room.width <= 24, "Room outside Tower");
                Require(rules.Floor(room.floor) != null, "Room on closed floor");
                for (int x = room.x; x < room.x + room.width; x++)
                    Require(rules.RoomAt(room.floor, x) == room, "Overlapping rooms");
            }
            if (slot > 1)
            {
                Require(state.introPhase == "complete", "Milestone not playable");
                Require(state.incidents.Count == 0 && !state.defeated, "Unsafe checkpoint");
                Require(state.residents.Count > 0, "Empty checkpoint");
            }
        }
        TowerState start = TowerMilestones.Create(1);
        var tutorial = new TowerRules(start);
        Require(tutorial.AwakenHeart() == null, "Heart tutorial failed");
        Require(tutorial.PlaceIntroGate() == null, "Gate tutorial failed");
        Require(tutorial.Build("house", 0, 21) == null, "Shack tutorial failed");
        Require(tutorial.ChooseStarter("kaela") == null, "Starter tutorial failed");
        Require(start.introPhase == "complete", "Tutorial did not finish");
        Require(tutorial.Build("house", 0, 12) != null, "Disconnected build accepted");
        Require(TowerMilestones.Create(10).floors.Count == 49, "Day 90 not fully opened");
        TowerState final = TowerMilestones.Create(10);
        foreach (TowerResident resident in final.residents)
            if (resident.origin == "hero")
                Require(new TowerRules(final).Room(resident.jobRoom).floor == 0,
                    "Roster hero not visible on the ground floor");
        foreach (string variant in new[] { "normal", "short", "tall", "muscle", "hourglass" })
            Require(final.residents.Exists(r => r.origin == "body" && r.chassisVariant == variant),
                "Celestium variant missing from day 90: " + variant);
        foreach (string unit in new[] { "kaela", "ghislaine", "elara", "helda", "daisy", "clarity", "amara",
            "body_normal", "body_short", "body_tall", "body_muscle", "body_hourglass" })
            foreach (string clip in new[] { "idle", "walk_in_place", "task", "knocked_down" })
            {
                Texture2D atlas = Resources.Load<Texture2D>("AdamsHaven/ChibiMotion/" + unit + "/" + clip);
                Require(atlas != null && atlas.width == 1024 && atlas.height == 1024,
                    "Missing or invalid chibi motion: " + unit + "/" + clip);
            }
        foreach (string unit in new[] { "kaela", "ghislaine", "elara", "helda", "daisy", "clarity", "amara",
            "body_normal", "body_short", "body_tall", "body_muscle", "body_hourglass" })
        {
            var probe = new GameObject("Tower chibi validation");
            probe.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                TowerChibiAnimator motion = probe.AddComponent<TowerChibiAnimator>();
                Require(motion.Configure(unit, true, false, Vector3.zero), "Chibi cannot configure: " + unit);
                Mesh mesh = probe.GetComponent<MeshFilter>().sharedMesh;
                Require(mesh != null && mesh.uv.Length == 4 &&
                    probe.GetComponent<MeshRenderer>().sharedMaterial.mainTexture != null,
                    "Chibi mesh or UV missing: " + unit);
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
        }
        var simulation = new TowerSimulationTests();
        simulation.InstantBuilds();
        try { RunSimulation(simulation); }
        finally { simulation.TimedBuilds(); }
        Debug.Log("Tower validation passed: ten checkpoints, tutorial, motion atlases, and eighteen simulation tests.");
    }

    private static void RunSimulation(TowerSimulationTests simulation)
    {
        simulation.StatMatchingAndPrioritiesChangeProduction();
        simulation.RushUsesMatchingSkillAndCreatesAResult();
        simulation.FamilyChildLivesAndMatures();
        simulation.FireCanBeResolvedInTheRoom();
        simulation.ExplorationReturnsDeterministicSupplies();
        simulation.ShortagesLowerHealthAndMorale();
        simulation.UnlockedHaulingCollectsReadyRoom();
        simulation.ResearchAndConstructionConsumeResources();
        simulation.GuidedOpeningTracksDecisionsAndIncidentRecovery();
        simulation.ManagedOpeningSurvivesTwentyMinutes();
        simulation.ManagedLateTowerRecoversFromShortageAndRaid();
        simulation.DownedResidentCanBeRescued();
        simulation.SickResidentReceivesCareAndConsumesATonic();
        simulation.HeartDestructionEndsTheRunImmediately();
        simulation.OfflineTimeNeverCreatesThreatsOrDownsResidents();
        simulation.VersionOneSaveMigratesWithoutLosingAssignments();
        simulation.AllMilestonesRemainPlayable();
        simulation.OlderPopulatedSaveSkipsTheFoundingGuide();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Tower validation: " + message);
    }
}
