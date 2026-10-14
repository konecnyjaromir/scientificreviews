using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace ScientificReviews.Helpers
{
    // Applies to normal text boxes, ToolStripTextBoxes and PropertyGrid editors in every form.
    public sealed class TextEditingShortcuts : IMessageFilter
    {
        private const int WM_KEYDOWN = 0x0100;
        private const int EM_REPLACESEL = 0x00C2;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, string text);

        public bool PreFilterMessage(ref Message message)
        {
            if (message.Msg != WM_KEYDOWN)
                return false;

            Control focused = Control.FromHandle(message.HWnd);
            TextBoxBase editor = focused as TextBoxBase;
            Keys keyData = (Keys)message.WParam.ToInt32() | Control.ModifierKeys;
            if (focused?.FindForm() is Forms.MainForm mainForm &&
                mainForm.TryHandleHistoryShortcut(keyData, editor))
                return true;

            if (keyData != (Keys.Control | Keys.Back))
                return false;
            if (editor == null)
                return false;

            DeletePreviousWord(editor);
            return true;
        }

        public static void DeletePreviousWord(TextBoxBase editor)
        {
            if (editor == null || editor.ReadOnly)
                return;

            if (editor.SelectionLength == 0)
            {
                int end = editor.SelectionStart;
                int start = end;
                string text = editor.Text;
                while (start > 0 && char.IsWhiteSpace(text[start - 1]))
                    start--;
                if (start > 0)
                {
                    bool word = IsWordCharacter(text[start - 1]);
                    while (start > 0 && !char.IsWhiteSpace(text[start - 1]) &&
                        IsWordCharacter(text[start - 1]) == word)
                        start--;
                }
                editor.Select(start, end - start);
            }

            // EM_REPLACESEL with TRUE makes the deletion undoable; SelectedText clears Undo.
            // https://learn.microsoft.com/en-us/windows/win32/controls/em-replacesel
            if (editor.SelectionLength > 0)
                SendMessage(editor.Handle, EM_REPLACESEL, new IntPtr(1), string.Empty);
        }

        private static bool IsWordCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';
    }
}
