using System;
using Autodesk.AutoCAD.EditorInput;

namespace C3DTools.Civil2021.Services;

internal static class CommandRunner
{
    public static void Run(Editor editor, Action action)
    {
        try
        {
            action();
        }
        catch (OperationCanceledException)
        {
            editor.WriteMessage("\nC3DTools: Đã hủy.");
        }
        catch (Autodesk.AutoCAD.Runtime.Exception ex)
        {
            editor.WriteMessage("\nC3DTools lỗi: " + ex.Message);
        }
        catch (System.Exception ex)
        {
            editor.WriteMessage("\nC3DTools lỗi: " + ex.Message);
        }
    }
}
