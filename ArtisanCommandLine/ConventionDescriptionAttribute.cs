using System;

namespace ArtisanCommandLine
{
    /// <summary>
    /// Add this attribute to add description to your command, option or argument for help page
    /// </summary>
    public class ConventionDescriptionAttribute : Attribute
    {
        /// <summary>
        /// Description text
        /// </summary>
        public string Text { get; set; }
        /// <inheritdoc/>
        /// <param name="text">Description text</param>
        public ConventionDescriptionAttribute(string text)
        {
            Text = text;
        }
    }
}