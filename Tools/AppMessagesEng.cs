using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Tools
{
    public class AppMessagesEng
    {
        // **Generic Error Messages**
        public const string GenericError = "An error occurred. Please try again.";

        public const string NetworkError = "Unable to connect. Check your internet connection.";

        public const string UnknownError = "An unknown error has occurred.";

        public const string UnauthorizedError = "You do not have permission to perform this action.";

        public const string ValidationError = "Some fields are invalid. Please check and try again.";

        public const string TotalMessage = "Total Variables:";

        public const string DatabaseUpdateError = "An error occurred while updating the database. Please try again later."; // NEW

        public const string DatabaseRestore = "Database has been restore.";

        public const string ConfirmationTitle = "Confirm Action";

        public const string RestoreConfirmationMessage = "Are you sure you want to restore the database? This action will overwrite all current data.";

        public const string Yes = "Yes";

        public const string No = "No";

        // **Messages Related to Prompts**
        public static class Prompts
        {
            public const string PromptDeleteError = "Unable to delete the prompt. Please try again.";

            public const string PromptEmptyTitleError = "The prompt title cannot be empty.";

            public const string PromptFilterError = "An issue occurred while filtering the prompts. Please try again.";

            public const string PromptLoadError = "An issue occurred while loading the prompts. Please try again.";

            public const string PromptNotFound = "The prompt was not found in the database.";

            public const string PromptSavedSuccess = "The prompt has been saved successfully.";

            public const string PromptSaveError = "An issue occurred while saving the prompt. Please try again.";

            public const string PromptVariablesError = "You must complete all variables before generating the prompt.";

            public const string PromptGenerateError = "An issue occurred while generating the prompt. Please try again.";

            public const string PromptEmptyAndUnSelected = "The prompt field is empty and no text is selected. Please try again.";

            public const string PromptDevelopmentMessage = "This feature is under development. We apologize for any inconvenience.";

            public const string PromptCopiedToClipboard = "The prompt has been copied to the clipboard.";

            public const string PromptVariableIsTheSameThanOther = "The selected variable is already in use within the prompt.";

            public const string PromptSharedError = "An error occurred while trying to share the prompt.";

            public const string PromptImportedSuccess = " The prompt has been imported successfully.";

            public const string PromptsDeletedSuccess = "The selected prompts have been deleted successfully.";
        }

        // **Warning Messages**
        public static class Warnings
        {
            public const string SelectWordError = "Select a word or phrase to convert it into a variable.";

            public const string WordAlreadyHasBraces = "The word or phrase is already surrounded by braces.";

            public const string WordHasNoBraces = "The selected word does not have braces around it to remove.";

            public const string InvalidTextSelection = "Please select a valid text.";

            public const string EmptySearch = "Please type a filter before continue.";
        }
    }
}