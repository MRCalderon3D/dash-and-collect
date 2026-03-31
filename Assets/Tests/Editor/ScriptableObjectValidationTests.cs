using NUnit.Framework;
using UnityEngine;
using DashAndCollect;

namespace DashAndCollect.Tests.Editor
{
    /// <summary>
    /// Edit-mode SO authoring validation (M2 exit criteria).
    ///
    /// Strategy:
    ///   Each test creates a minimal SO instance via CreateInstance, intentionally
    ///   leaves one required field at its broken default, then asserts the violation.
    ///   Tests do NOT call OnValidate — they verify the same contract OnValidate
    ///   enforces: that a correctly-authored SO satisfies all invariants readable
    ///   from C# without entering Play Mode.
    ///
    ///   Tests are grouped by SO type. All instances are destroyed in TearDown.
    ///
    /// Naming: TypeName_Field_Condition_ExpectedBehaviour
    /// </summary>
    [TestFixture]
    public class ScriptableObjectValidationTests
    {
        // ── SpawnConfig ───────────────────────────────────────────────────────

        [Test]
        public void SpawnConfig_InitialSpeed_DefaultIsPositive()
        {
            var so = ScriptableObject.CreateInstance<SpawnConfig>();
            try   { Assert.Greater(so.initialSpeed, 0f, "SpawnConfig.initialSpeed default must be > 0."); }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void SpawnConfig_MaxSpeed_DefaultIsGreaterThanInitialSpeed()
        {
            var so = ScriptableObject.CreateInstance<SpawnConfig>();
            try   { Assert.GreaterOrEqual(so.maxSpeed, so.initialSpeed, "SpawnConfig.maxSpeed must be >= initialSpeed."); }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void SpawnConfig_LookAheadDistance_DefaultIsPositive()
        {
            var so = ScriptableObject.CreateInstance<SpawnConfig>();
            try   { Assert.Greater(so.lookAheadDistance, 0f, "SpawnConfig.lookAheadDistance default must be > 0."); }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void SpawnConfig_PoolSizePerChunk_DefaultIsAtLeastOne()
        {
            var so = ScriptableObject.CreateInstance<SpawnConfig>();
            try   { Assert.GreaterOrEqual(so.poolSizePerChunk, 1, "SpawnConfig.poolSizePerChunk default must be >= 1."); }
            finally { Object.DestroyImmediate(so); }
        }

        // ── ScoreConfig ───────────────────────────────────────────────────────

        [Test]
        public void ScoreConfig_BasePickupScore_DefaultIsNonNegative()
        {
            var so = ScriptableObject.CreateInstance<ScoreConfig>();
            try   { Assert.GreaterOrEqual(so.basePickupScore, 0, "ScoreConfig.basePickupScore default must be >= 0."); }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void ScoreConfig_ChainBonusScore_DefaultIsNonNegative()
        {
            var so = ScriptableObject.CreateInstance<ScoreConfig>();
            try   { Assert.GreaterOrEqual(so.chainBonusScore, 0, "ScoreConfig.chainBonusScore default must be >= 0."); }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void ScoreConfig_CoinsPerChain_DefaultIsNonNegative()
        {
            var so = ScriptableObject.CreateInstance<ScoreConfig>();
            try   { Assert.GreaterOrEqual(so.coinsPerChain, 0, "ScoreConfig.coinsPerChain default must be >= 0."); }
            finally { Object.DestroyImmediate(so); }
        }

        // ── LaneConfig ────────────────────────────────────────────────────────

        [Test]
        public void LaneConfig_LanePositions_DefaultHasExactlyThreeLanes()
        {
            var so = ScriptableObject.CreateInstance<LaneConfig>();
            try   { Assert.AreEqual(3, so.lanePositions.Length, "LaneConfig.lanePositions must have exactly 3 entries."); }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void LaneConfig_LanePositions_DefaultIsNotNull()
        {
            var so = ScriptableObject.CreateInstance<LaneConfig>();
            try   { Assert.IsNotNull(so.lanePositions, "LaneConfig.lanePositions must not be null."); }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void LaneConfig_LanePositions_DefaultCenterLaneIsAtOrigin()
        {
            var so = ScriptableObject.CreateInstance<LaneConfig>();
            try   { Assert.AreEqual(0f, so.lanePositions[1], 0.001f, "LaneConfig lane 1 (center) must be at x=0 by default."); }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void LaneConfig_LanePositions_DefaultLanesAreSymmetric()
        {
            var so = ScriptableObject.CreateInstance<LaneConfig>();
            try
            {
                Assert.AreEqual(-so.lanePositions[2], so.lanePositions[0], 0.001f,
                    "LaneConfig lanes 0 and 2 must be symmetric around the center lane.");
            }
            finally { Object.DestroyImmediate(so); }
        }

        // ── PlayerConfig ──────────────────────────────────────────────────────

        [Test]
        public void PlayerConfig_DashDuration_DefaultIsPositive()
        {
            var so = ScriptableObject.CreateInstance<PlayerConfig>();
            try   { Assert.Greater(so.dashDuration, 0f, "PlayerConfig.dashDuration default must be > 0."); }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void PlayerConfig_RecoveryDuration_DefaultIsNonNegative()
        {
            var so = ScriptableObject.CreateInstance<PlayerConfig>();
            try   { Assert.GreaterOrEqual(so.recoveryDuration, 0f, "PlayerConfig.recoveryDuration default must be >= 0."); }
            finally { Object.DestroyImmediate(so); }
        }

        // ── ChunkDefinition ───────────────────────────────────────────────────

        [Test]
        public void ChunkDefinition_Height_DefaultIsPositive()
        {
            var so = ScriptableObject.CreateInstance<ChunkDefinition>();
            try   { Assert.Greater(so.height, 0f, "ChunkDefinition.height default must be > 0."); }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void ChunkDefinition_Tags_DefaultIsNotNull()
        {
            var so = ScriptableObject.CreateInstance<ChunkDefinition>();
            try   { Assert.IsNotNull(so.tags, "ChunkDefinition.tags must not be null (may be empty)."); }
            finally { Object.DestroyImmediate(so); }
        }
    }
}
