using NUnit.Framework;

using UnityEngine;
using UnityEngine.TestTools;

namespace PacManGame.Tests.EditMode
{
    /// <summary>
    /// Covers: "Model loads successfully", "Worker is created", and
    /// "Exception handling".
    ///
    /// These tests load the real ghost_ai.onnx produced by train_ghost_ai.py.
    /// To run them:
    ///   1. Run train_ghost_ai.py to produce ghost_ai.onnx
    ///   2. Copy it to: Assets/Tests/EditMode/Resources/TestModels/ghost_ai.onnx
    ///      (Unity auto-imports it as a ModelAsset there)
    ///
    /// If the file isn't present, the tests that need it report
    /// "Inconclusive" rather than failing, so this suite doesn't block a
    /// CI run before a model has been trained -- but the exception-handling
    /// tests (which deliberately test the *missing* model case) always run.
    /// </summary>
    public class GhostModelLoadingTests
    {
        private GameObject ghostObject;
        private GhostControllerNN controller;
        private Unity.InferenceEngine.ModelAsset testModel;

        [SetUp]
        public void SetUp()
        {
            testModel = Resources.Load<Unity.InferenceEngine.ModelAsset>("TestModels/ghost_ai");

            ghostObject = new GameObject("TestGhost");
            ghostObject.AddComponent<Rigidbody2D>();
            controller = ghostObject.AddComponent<GhostControllerNN>();
        }

        [TearDown]
        public void TearDown()
        {
            controller.Dispose();
            Object.DestroyImmediate(ghostObject);
        }

        // ---- Model loads successfully --------------------------------------

        // ModelLoads_Successfully_WhenAssetIsValid() explanation:
        // This test verifies that, assuming the model is not null, the provided model successfully loads.

        [Test]
        public void ModelLoads_Successfully_WhenAssetIsValid()
        {
            Assume.That(testModel, Is.Not.Null,
                "No test model at Resources/TestModels/ghost_ai.onnx -- " +
                "run train_ghost_ai.py and copy the .onnx in to exercise this test.");

            controller.modelAsset = testModel;
            controller.InitializeModel();

            Assert.IsTrue(controller.IsModelLoaded,
                "Expected IsModelLoaded to be true after loading a valid model asset.");
        }

        // InitializeModel_CanBeCalledMultipleTimes_WithoutLeakingOrThrowing() explanation:
        // This test verifies that, assuming the provided model is not null, initializing the model multiple times does not cause any issues such as throwing an exception.

        [Test]
        public void InitializeModel_CanBeCalledMultipleTimes_WithoutLeakingOrThrowing()
        {
            Assume.That(testModel, Is.Not.Null,
                "No test model found -- see ModelLoads_Successfully_WhenAssetIsValid.");

            controller.modelAsset = testModel;

            Assert.DoesNotThrow(() =>
            {
                controller.InitializeModel();
                controller.InitializeModel(); // re-init should dispose the old worker first, not double-allocate
                controller.InitializeModel();
            });

            Assert.IsTrue(controller.IsModelLoaded);
            Assert.IsTrue(controller.HasWorker);
        }

        // ---- Worker is created ----------------------------------------------

        // Worker_Is_Created_WhenModelLoadsSuccessfully() explanation:
        // This test verifies that, assuming the provided model is not null, a worker is successfully created for the ghost controller.

        [Test]
        public void Worker_Is_Created_WhenModelLoadsSuccessfully()
        {
            Assume.That(testModel, Is.Not.Null,
                "No test model found -- see ModelLoads_Successfully_WhenAssetIsValid.");

            controller.modelAsset = testModel;
            controller.InitializeModel();

            Assert.IsTrue(controller.HasWorker,
                "Expected a Worker to be created once the model loads successfully.");
        }

        // Worker_Is_Disposed_OnComponentDestroy() explanation:
        // This test verifies that, assuming the provided model is not null, when a ghost controller is disposed, not exception is thrown and the associated worker is also cleanly released.

        [Test]
        public void Worker_Is_Disposed_OnComponentDestroy()
        {
            Assume.That(testModel, Is.Not.Null,
                "No test model found -- see ModelLoads_Successfully_WhenAssetIsValid.");

            controller.modelAsset = testModel;
            controller.InitializeModel();
            Assert.IsTrue(controller.HasWorker);

            Assert.DoesNotThrow(() => controller.Dispose(),
                "Disposing should cleanly release the Sentis worker's native resources.");
            Assert.IsFalse(controller.HasWorker);
        }

        // ---- Exception handling ----------------------------------------------

        // InitializeModel_HandlesMissingAsset_WithoutThrowing() explanation:
        // This test verifies that, in a case of a corrupted/missing model asset, the game should not throw an exception (it should have fallback behavior).

        [Test]
        public void InitializeModel_HandlesMissingAsset_WithoutThrowing()
        {
            controller.modelAsset = null;

            Assert.DoesNotThrow(() => controller.InitializeModel(),
                "A missing ModelAsset should degrade to fallback behavior, not throw.");

            Assert.IsFalse(controller.IsModelLoaded);
            Assert.IsFalse(controller.HasWorker);
        }

        // InitializeModel_LogsWarning_WhenAssetIsMissing() explanation:
        // This test verifies that, in a case of a corrupted/missing model asset, the game outputs a warning log stating that there was an issue and fallback behavior has taken place.

        [Test]
        public void InitializeModel_LogsWarning_WhenAssetIsMissing()
        {
            controller.modelAsset = null;

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "No ModelAsset assigned.*falling back to random movement"));

            controller.InitializeModel();
        }

        // InitializeModel_LogsWarning_WhenAssetIsMissing() explanation:
        // This test verifies that, in a case of a corrupted/missing model asset and that IntializeModel() was still called, when the ghost controller calls Dispose(), it should not throw an exception.

        [Test]
        public void Dispose_IsSafeToCall_WhenNoWorkerWasEverCreated()
        {
            // Component was created but InitializeModel() never ran with a
            // valid asset -- Dispose() must still be a no-op, not a crash.
            Assert.DoesNotThrow(() => controller.Dispose());
        }

        // Dispose_IsSafeToCall_Repeatedly() explanation:
        // This test verifies that the ghost controller can call Dispose() multiple times without throwing an exception.

        [Test]
        public void Dispose_IsSafeToCall_Repeatedly()
        {
            controller.modelAsset = null;
            controller.InitializeModel();

            Assert.DoesNotThrow(() =>
            {
                controller.Dispose();
                controller.Dispose();
                controller.Dispose();
            });
        }
    }
}
