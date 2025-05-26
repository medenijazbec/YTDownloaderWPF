using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ytDownloaderWPF.Classes
{
    /// <summary>
    /// Helper class to store a display string and an underlying value for ComboBox items.
    /// </summary>
    public class ComboBoxItemWrapper
    {
        public string Value { get; set; } //this will be the full format string
        public string DisplayText { get; set; }

        public ComboBoxItemWrapper(string value, string displayText)
        {
            Value = value;
            DisplayText = displayText;
        }

        public override string ToString() => DisplayText;
    }

}
