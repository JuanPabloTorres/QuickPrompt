# Quick Prompt

**Quick Prompt** is a modern and flexible application for creating, organizing, and executing smart *prompts* using popular AI engines such as **ChatGPT**, **Gemini**, **Grok** or **Copilot**. 
It's designed for writers, developers, students, content creators, and professionals who need to quickly generate ideas, automate tasks, or dynamically translate content.

---

## 🚀 Key Features

- ✨ **Advanced Prompt Management**
  - Create, edit, and save reusable prompts.
  - Organize prompts by usage date, favorites, and custom categories.
  - Use dynamic variables to adapt prompts to different contexts.

- 🧠 **Integration with Popular AI Engines**
  - Send your prompts directly to ChatGPT, Gemini, or Grok using the built-in WebView.
  - Copy the completed prompt for use in any other platform or tool.

- 📅 **Date Filtering**
  - Special views for prompts used **today** or within the **last 7 days**.

- 💬 **Finalized Prompts**
  - Save completed prompts with the user-filled values.
  - Mark final prompts as favorites for quick access to key results.

- 🧩 **Smart Suggestions**
  - Cache system that remembers previously used variable values.
  - Dynamic auto-suggestions based on usage history.

- 🎨 **Modern Interface**
  - Clean, intuitive, and responsive UI built with .NET MAUI.
  - Visual mode with interactive chips for quick variable editing.

- 🌐 **Cross-Platform Support**
  - Available on Android. Coming soon to iOS, Windows, and MacOS.

---

## 🛠️ Technologies Used

- **Frontend:** .NET MAUI (C#)
- **Local Backend:** SQLite (local data persistence)
- **Architecture Pattern:** MVVM (with CommunityToolkit.Mvvm)
- **Internal Messaging:** WeakReferenceMessenger
- **External Libraries:** Lottie, MediaPicker, System.Text.Json, etc.

---

## 📦 Project Structure

```plaintext
QuickPrompt/
│
├── Models/
│   ├── PromptTemplate.cs
│   ├── FinalPrompt.cs
│   └── PromptVariableCache.cs
│
├── ViewModels/
│   ├── MainPageViewModel.cs
│   ├── PromptDetailsPageViewModel.cs
│   └── SettingsViewModel.cs
│
├── Pages/
│   ├── MainPage.xaml
│   ├── PromptDetailsPage.xaml
│   ├── AiWebViewPage.xaml
│   └── TodayPromptsPage.xaml
│
├── Services/
│   ├── IPromptRepository.cs
│   ├── IFinalPromptRepository.cs
│   └── PromptVariableCacheService.cs
│
├── Resources/
│   ├── Styles/
│   ├── LottieAnimations/
│   └── Icons/
│
├── App.xaml
└── AppShell.xaml

🧪 How to Use
Create a Prompt

Tap “New Prompt” and enter your custom text.

Use variable placeholders using curly braces: <name>, <topic>, etc.

Fill in the Variables

When running the prompt, the app will detect variables and ask you to complete them.

Use the Prompt

Press “Use Prompt” to launch it in ChatGPT, Gemini, or Grok (via WebView).

Alternatively, copy it to your clipboard.

Save Final Output

After filling in values, you can save the final version for future reference.

View by Date

Switch to Today or Last 7 Days views to track recent prompts.

Mark Favorites

Long-press or tap the star icon to mark prompts you use often.

🧩 Prompt Variables Example
Prompt Template:

css
Copy
Edit
Write a professional email to <recipient> explaining the <issue> and proposing <solution>.
When run:

You'll be prompted to enter values for:

<recipient> → e.g., "the customer support team"

<issue> → e.g., "a billing discrepancy"

<solution> → e.g., "a refund or invoice correction"

Final Prompt:

css
Copy
Edit
Write a professional email to the customer support team explaining the billing discrepancy and proposing a refund or invoice correction.
🌟 Contribution Guidelines
We welcome contributions! Whether it's bug fixes, feature ideas, or UI suggestions, here's how to contribute:

Fork the repository

Create a branch

bash
Copy
Edit
git checkout -b feature/my-new-feature
Commit your changes

bash
Copy
Edit
git commit -m "Add: My new feature"
Push to the branch

bash
Copy
Edit
git push origin feature/my-new-feature
Open a Pull Request

Provide a clear description of what you changed and why.

Please make sure your code is clean and follows MVVM best practices for .NET MAUI.

