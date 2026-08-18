using NUnit.Framework;

namespace PacManGame.Tests.EditMode
{
    /// <summary>
    /// Covers: "Output values are correct" and "Invalid input handling".
    ///
    /// These exercise GhostControllerNN.EncodeState() and .SelectDirection()
    /// directly as pure functions -- no Sentis worker, no GameObject, no
    /// trained model required, so they run in milliseconds and can't be
    /// flaky.
    /// </summary>
    public class GhostInferenceLogicTests
    {
        // ==================== EncodeState ====================

        // EncodeState_ProducesExpectedVectorLength() explanation:
        // The EncodeState function of the GhostControllerNN converts input into the proper format expected by the neural network. This test simply verifies that the outputted result is the proper length of 11.
        
        [Test]
        public void EncodeState_ProducesExpectedVectorLength()
        {
            bool[] canMove = { true, false, true, false };
            float[] result = GhostControllerNN.EncodeState(canMove, 0.1f, -0.2f, false, -1);

            Assert.AreEqual(11, result.Length);
        }

        // EncodeState_CorrectlyEncodesWallMaskAndPlayerOffset() explanation:
        // The EncodeState function of the GhostControllerNN converts input into the proper format expected by the neural network. This test verifies if the resulting array of floats matches the expected output based on the given initial input parameters

        [Test]
        public void EncodeState_CorrectlyEncodesWallMaskAndPlayerOffset()
        {
            bool[] canMove = { true, false, true, true };
            float[] result = GhostControllerNN.EncodeState(canMove, 0.25f, -0.5f, true, 2);

            Assert.AreEqual(1f, result[0], "index 0 (up) should mirror canMove[0]");
            Assert.AreEqual(0f, result[1], "index 1 (down) should mirror canMove[1]");
            Assert.AreEqual(1f, result[2], "index 2 (left) should mirror canMove[2]");
            Assert.AreEqual(1f, result[3], "index 3 (right) should mirror canMove[3]");
            Assert.AreEqual(0.25f, result[4], "dx should pass through unchanged");
            Assert.AreEqual(-0.5f, result[5], "dy should pass through unchanged");
            Assert.AreEqual(1f, result[6], "power flag should be 1 when true");

            CollectionAssert.AreEqual(
                new[] { 0f, 0f, 1f, 0f },
                new[] { result[7], result[8], result[9], result[10] },
                "direction one-hot should mark index 2 (left) and nothing else");
        }

        // EncodeState_PowerFlagIsZero_WhenPowerModeIsFalse() explanation:
        // The EncodeState function of the GhostControllerNN converts input into the proper format expected by the neural network. This test verifies that the provided powerMode part is successfully encoded to a 0f when passed as false.

        [Test]
        public void EncodeState_PowerFlagIsZero_WhenPowerModeIsFalse()
        {
            bool[] canMove = { true, true, true, true };
            float[] result = GhostControllerNN.EncodeState(canMove, 0f, 0f, false, -1);

            Assert.AreEqual(0f, result[6]);
        }

        // EncodeState_NoDirection_LeavesOneHotAllZero() explanation:
        // The EncodeState function of the GhostControllerNN converts input into the proper format expected by the neural network. This test verifies that when no direction is given (2nd and 3rd parameter of EncodeState() is 0f), then the last four floats in the result array should be 0f.

        [Test]
        public void EncodeState_NoDirection_LeavesOneHotAllZero()
        {
            bool[] canMove = { true, true, true, true };
            float[] result = GhostControllerNN.EncodeState(canMove, 0f, 0f, false, -1);

            CollectionAssert.AreEqual(
                new[] { 0f, 0f, 0f, 0f },
                new[] { result[7], result[8], result[9], result[10] });
        }

        // ---- Invalid input handling ----

        // EncodeState_ThrowsOnWrongLengthCanMoveArray() explanation:
        // The EncodeState function of the GhostControllerNN converts input into the proper format expected by the neural network. This test verifies that the first paramater, an array of booleans, is of the the proper length.

        [Test]
        public void EncodeState_ThrowsOnWrongLengthCanMoveArray()
        {
            bool[] tooShort = { true, false, true }; // only 3 entries, needs 4
            Assert.Throws<System.ArgumentException>(() =>
                GhostControllerNN.EncodeState(tooShort, 0f, 0f, false, -1));
        }

        // EncodeState_ThrowsOnNullCanMoveArray() explanation:
        // The EncodeState function of the GhostControllerNN converts input into the proper format expected by the neural network. This test verifies that an exception is thrown when a null array is provided as the first parameter.

        [Test]
        public void EncodeState_ThrowsOnNullCanMoveArray()
        {
            Assert.Throws<System.ArgumentException>(() =>
                GhostControllerNN.EncodeState(null, 0f, 0f, false, -1));
        }

        // EncodeState_IgnoresOutOfRangeDirectionIndex() explanation:
        // The EncodeState function of the GhostControllerNN converts input into the proper format expected by the neural network. This test verifies that an exception is not thrown when a direction index is out of range

        [Test]
        public void EncodeState_IgnoresOutOfRangeDirectionIndex()
        {
            bool[] canMove = { true, true, true, true };
            float[] result = null;

            Assert.DoesNotThrow(() =>
                result = GhostControllerNN.EncodeState(canMove, 0f, 0f, false, 99));

            CollectionAssert.AreEqual(
                new[] { 0f, 0f, 0f, 0f },
                new[] { result[7], result[8], result[9], result[10] },
                "an out-of-range direction index should be ignored rather than throw or corrupt the vector");
        }

        // ==================== SelectDirection ====================

        // SelectDirection_PicksHighestScoringValidMove() explanation:
        // The SelectDirection function of the GhostControllerNN outputs the highest-scoring direction given specific parameters. This test verifies that the resulting direction is the expected one given the initial parameters.

        [Test]
        public void SelectDirection_PicksHighestScoringValidMove()
        {
            float[] logits = { 0.1f, 0.9f, 0.4f, 0.2f }; // "down" (index 1) is highest
            bool[] validMoves = { true, true, true, true };

            int chosen = GhostControllerNN.SelectDirection(logits, validMoves, oppositeIndex: -1);

            Assert.AreEqual(1, chosen, "should choose 'down', the highest-scoring valid move");
        }

        // SelectDirection_ExcludesWalledOffDirections() explanation:
        // The SelectDirection function of the GhostControllerNN outputs the highest-scoring direction given specific parameters. This test verifies that the resulting direction is the expected one given the initial parameters when the actual best direction is blocked by a wall

        [Test]
        public void SelectDirection_ExcludesWalledOffDirections()
        {
            float[] logits = { 0.9f, 0.1f, 0.2f, 0.3f }; // "up" scores highest but is walled off
            bool[] validMoves = { false, true, true, true };

            int chosen = GhostControllerNN.SelectDirection(logits, validMoves, oppositeIndex: -1);

            Assert.AreNotEqual(0, chosen, "should never choose a direction blocked by a wall");
            Assert.AreEqual(3, chosen, "should fall through to the next-highest valid score ('right')");
        }

        // SelectDirection_AvoidsReversingUnlessDeadEnd() explanation:
        // The SelectDirection function of the GhostControllerNN outputs the highest-scoring direction given specific parameters. This test verifies that the next chosen direction is not the opposite of the current direction unless it's a dead end.

        [Test]
        public void SelectDirection_AvoidsReversingUnlessDeadEnd()
        {
            // "up" (0) scores highest; ghost is currently moving "up", so its
            // reverse is "down" (1) and must be excluded even though nothing
            // stops it physically.
            float[] logits = { 0.9f, 0.5f, 0.1f, 0.2f };
            bool[] validMoves = { true, true, true, true };

            int chosen = GhostControllerNN.SelectDirection(logits, validMoves, oppositeIndex: 1);

            Assert.AreEqual(0, chosen, "should pick 'up', the highest-scoring non-reversing move");
        }

        // SelectDirection_ForcesReverseAtDeadEnd() explanation:
        // The SelectDirection function of the GhostControllerNN outputs the highest-scoring direction given specific parameters. This test verifies that the next chosen direction is reverse if it is the only valid move (dead end).

        [Test]
        public void SelectDirection_ForcesReverseAtDeadEnd()
        {
            float[] logits = { 0.9f, 0.5f, 0.1f, 0.2f };
            bool[] validMoves = { false, true, false, false }; // only the reverse direction is open

            int chosen = GhostControllerNN.SelectDirection(logits, validMoves, oppositeIndex: 1);

            Assert.AreEqual(1, chosen,
                "at a dead end the ghost must reverse even though that direction is excluded by default");
        }

        // SelectDirection_IgnoresNaNAndInfiniteLogits() explanation:
        // The SelectDirection function of the GhostControllerNN outputs the highest-scoring direction given specific parameters. This test verifies that infinity and NaN entries from the AI model are ignored.

        [Test]
        public void SelectDirection_IgnoresNaNAndInfiniteLogits()
        {
            float[] logits = { float.NaN, float.PositiveInfinity, 0.3f, float.NegativeInfinity };
            bool[] validMoves = { true, true, true, true };

            int chosen = GhostControllerNN.SelectDirection(logits, validMoves, oppositeIndex: -1);

            Assert.AreEqual(2, chosen,
                "NaN/Infinity entries from a corrupted model output should be skipped, leaving the one finite score");
        }

        // ---- Invalid input handling ----

        // SelectDirection_ReturnsMinusOne_OnWrongLengthLogitsArray() explanation:
        // The SelectDirection function of the GhostControllerNN outputs the highest-scoring direction given specific parameters. This test verifies that if the logits (array of floats) provided is incomplete, it should output -1 (no decision instead of throwing an exception).

        [Test]
        public void SelectDirection_ReturnsMinusOne_OnWrongLengthLogitsArray()
        {
            float[] tooShort = { 0.1f, 0.2f, 0.3f }; // only 3 entries, needs 4
            bool[] validMoves = { true, true, true, true };

            int chosen = GhostControllerNN.SelectDirection(tooShort, validMoves, oppositeIndex: -1);

            Assert.AreEqual(-1, chosen,
                "malformed model output should signal 'no decision' rather than throw or index out of range");
        }

        // SelectDirection_ReturnsMinusOne_OnNullLogits() explanation:
        // The SelectDirection function of the GhostControllerNN outputs the highest-scoring direction given specific parameters. This test verifies that if the logits (array of floats) provided is null, it should output -1 (no decision instead of throwing an exception).

        [Test]
        public void SelectDirection_ReturnsMinusOne_OnNullLogits()
        {
            bool[] validMoves = { true, true, true, true };
            int chosen = GhostControllerNN.SelectDirection(null, validMoves, oppositeIndex: -1);
            Assert.AreEqual(-1, chosen);
        }

        // SelectDirection_ReturnsMinusOne_OnWrongLengthValidMovesArray() explanation:
        // The SelectDirection function of the GhostControllerNN outputs the highest-scoring direction given specific parameters. This test verifies that if the validMoves (array of booleans) provided is incomplete, it should output -1 (no decision instead of throwing an exception).

        [Test]
        public void SelectDirection_ReturnsMinusOne_OnWrongLengthValidMovesArray()
        {
            float[] logits = { 0.1f, 0.2f, 0.3f, 0.4f };
            bool[] tooShort = { true, true }; // only 2 entries, needs 4

            int chosen = GhostControllerNN.SelectDirection(logits, tooShort, oppositeIndex: -1);

            Assert.AreEqual(-1, chosen);
        }

        // SelectDirection_ReturnsMinusOne_WhenNoValidMovesExist() explanation:
        // The SelectDirection function of the GhostControllerNN outputs the highest-scoring direction given specific parameters. This test verifies that if the validMoves (array of booleans) provided has no possible moves (all false), it should output -1 (no decision instead of throwing an exception).

        [Test]
        public void SelectDirection_ReturnsMinusOne_WhenNoValidMovesExist()
        {
            float[] logits = { 0.1f, 0.2f, 0.3f, 0.4f };
            bool[] validMoves = { false, false, false, false }; // fully boxed in, no reverse to fall back on either

            int chosen = GhostControllerNN.SelectDirection(logits, validMoves, oppositeIndex: -1);

            Assert.AreEqual(-1, chosen,
                "with no walkable neighbor at all, the caller (not this pure function) decides the recovery behavior");
        }

        // SelectDirection_DoesNotThrow_OnOutOfRangeOppositeIndex() explanation:
        // The SelectDirection function of the GhostControllerNN outputs the highest-scoring direction given specific parameters. This test verifies that if the provided oppositeIndex parameter is out of range, do not throw an exception.


        [Test]
        public void SelectDirection_DoesNotThrow_OnOutOfRangeOppositeIndex()
        {
            float[] logits = { 0.1f, 0.2f, 0.3f, 0.4f };
            bool[] validMoves = { true, true, true, true };

            int chosen = -99;
            Assert.DoesNotThrow(() =>
                chosen = GhostControllerNN.SelectDirection(logits, validMoves, oppositeIndex: 7));

            Assert.AreEqual(3, chosen, "an out-of-range oppositeIndex should simply never match, not throw");
        }
    }
}
