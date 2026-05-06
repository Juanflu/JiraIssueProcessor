# Jira Issue Processor

A .NET utility to process and minify Jira issue JSON exports. It removes unnecessary fields, empty values, and boilerplate text, while filtering the changelog to only keep status changes. Ideal for reducing token count for LLM processing.


## Features

- **Property Removal**: Strips a predefined list of verbose and unnecessary properties from the JSON.
- **Value Cleaning**: Removes properties that are null, empty strings, or empty arrays.
- **Template Field Cleaning**: Detects and removes custom fields that contain only boilerplate template text and no user-entered content.
- **Changelog Optimization**: Filters the issue's history to retain only status change events, significantly reducing the size of the changelog data.
- **Minified Output**: Exports the cleaned JSON in a compact, single-line format.


## Getting Started

Follow these steps to set up and run the application.

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download).
- A Jira Personal Access Token.
- A Jira JQL Query to work with.

### Usage

1. **Clone the Repository**
   
    ```bash
    git clone <repository-url>
    ```
    
    ```bash
    cd JiraIssueProcessor
    ```
   
3. **Generate your Jira Personal Access Token (PAT)**
   - Go to the Personal Access Tokens section in your profile: https://{YOUR_JIRA_INSTANCE}/secure/ViewProfile.jspa?selectedTab=com.atlassian.pats.pats-plugin:jira-user-personal-access-tokens
   - Create a new token and save it securely.

4. **Export your JSON from Jira API**
   - Using [Bruno](https://www.usebruno.com/) or any other API Client, get the export of the Jira query that you want.
  
   ```
   curl --request GET \
   --url 'https://{YOUR_JIRA_INSTANCE}/rest/api/2/search?expand=changelog&maxResults=150&jql={YOUR_JQL_QUERY}' \
   --header 'authorization: Bearer {YOUR_JIRA_PAT}'
   ```
 
5. **Prepare Your Input File**
   - Place your Jira export JSON export file in the root of the project directory. For this example, let's assume your file is named `my-jira-export.json`.

6. **Update File Paths in the Code**
   - Open the `Program.cs` file.
   - Locate the `Main` method.
   - Change the `inputFilePath` and `outputFilePath` variables to match your input file and desired output file name.

   ```csharp
   private static void Main()
   {
       const string inputFilePath = "my-jira-export.json";
       const string outputFilePath = "my-jira-export-minified.json";

       var jsonString = File.ReadAllText(inputFilePath);
       // ...
   }
   ```

7. **Run the Application**
   - Execute the following command from your terminal in the project's root directory:
   ```bash
   dotnet run
   ```

8. **Find the Output**
   - The application will generate a minified JSON file (e.g., `my-jira-export-minified.json`) in the same directory. This file contains the processed and cleaned data.


## Customization

You can easily customize the cleaning process by modifying the `HashSet` collections at the top of `Program.cs`:

- `PropertiesToRemove`: Add or remove JSON property keys that you want to be stripped from the output.
- `TemplateFields`: Add the custom field IDs (e.g., `customfield_12345`) that you want the application to check for empty boilerplate content.
- `templateWords` (in `IsEmptyTemplate` method): Add or remove keywords to improve the detection of your specific Jira templates.


## Prompting

Once the minimized file is ready, then is time to analyze the data that contains. For this purpose you can use any AI Tool as [Gemini](https://gemini.google.com/) or [NotebookLM](https://notebooklm.google.com/).
Here you have some examples of prompts:


> Analyze the cycle time of these tickets. Calculate the average time from when a ticket moves to 'In Progress' until it reaches 'Done'. Identify the tickets with the highest cycle time and provide possible causes based on their transition history.

> How many tickets have returned to 'In Progress' from 'In Review' or from 'Blocked'? List those tickets, how many times it occurred for each one, and the total time lost due to that rework.

> Calculate the average time tickets have spent in each column of the board (To Do, In Progress, In Review, Blocked, Done). Which column represents the biggest bottleneck?

> Generate an executive summary of the team's delivery status based on this data. Include: team velocity, process health, main risks identified, and a specific recommendation for the next sprint.
