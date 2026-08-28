namespace KINEMATION.Shared.KAnimationCore.Editor.Misc
{
    public interface IEditorTool
    {
        void Init();
        void Render();
        string GetToolName();
        string GetToolCategory();
        string GetToolDescription();
        string GetDocsURL();
    }
}
