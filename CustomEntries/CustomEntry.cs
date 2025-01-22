using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.CustomEntries
{
    public class CustomEntry : Entry
    {
        public int SelectionStart { get; set; }
        public int SelectionLength { get; set; }

        public event EventHandler SelectionChanged;

        public void UpdateSelection(int start, int length)
        {
            SelectionStart = start;

            SelectionLength = length;

            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }

        public string GetSelectedText()
        {
            if (SelectionLength > 0 && !string.IsNullOrEmpty(Text))
            {
                return Text.Substring(SelectionStart, SelectionLength);
            }
            return string.Empty;
        }
    }

}
