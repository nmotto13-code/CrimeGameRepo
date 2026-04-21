namespace CasebookGame.Core
{
    public enum GameState
    {
        Title,
        CaseIntro,
        Investigation,   // player finding evidence on scene
        Analysis,        // player reviewing evidence + claims tabs
        Submitting,      // waiting for claim tap
        Result
    }
}
