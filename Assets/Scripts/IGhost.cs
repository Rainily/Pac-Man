/// <summary>
/// Implemented by both GhostController (heuristic) and GhostControllerNN
/// (Sentis-driven) so PacManController doesn't need to care which kind
/// of ghost it just touched.
/// </summary>
public interface IGhost
{
    void GetEaten();
}
