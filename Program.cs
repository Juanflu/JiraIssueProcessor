using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace JiraIssueProcessor
{
    internal abstract class Program
    {
        private static readonly HashSet<string> PropertiesToRemove =
        [
            "avatarUrls", "customfield_11800", "progress", "votes",
            "customfield_17469", "projectCategory", "customfield_10005", "customfield_10004",
            "customfield_13000", "self", "iconUrl", "avatarId", "lastViewed",
            "watches", "workratio", "aggregateprogress",
            "renderedFields", "editmeta", "operations", "versionedRepresentations",
            "emailAddress", "timeZone", "active", "accountId", "accountType"
        ];

        private static readonly HashSet<string> TemplateFields =
        [
            "customfield_10902", "customfield_21337", "customfield_21335",
            "customfield_21320", "customfield_21301"
        ];

        private static void Main()
        {
            const string inputFilePath = "{inputfile}.json";
            const string outputFilePath = "{outputfile}.json";

            var jsonString = File.ReadAllText(inputFilePath);
            var rootNode = JsonNode.Parse(jsonString);

            OptimizeChangelogs(rootNode!);

            CleanJsonNode(rootNode!);

            // Export in a single line to save tokens
            var options = new JsonSerializerOptions { WriteIndented = false };
            File.WriteAllText(outputFilePath, rootNode!.ToJsonString(options));
        }

        
        /// <summary>
        /// Recursively traverses a JSON structure to find and optimize 'changelog' objects.
        /// This method identifies 'changelog' properties within the JSON and applies the <see cref="FilterStatusChangesOnly"/>
        /// method to them, effectively reducing the changelog data to only status change events.
        /// </summary>
        /// <param name="node">The <see cref="JsonNode"/> to traverse. The optimization is performed in-place on this node and its descendants.</param>
        private static void OptimizeChangelogs(JsonNode node)
        {
            if (node is JsonObject jsonObject)
            {
                if (jsonObject.TryGetPropertyValue("changelog", out var changelogNode) &&
                    changelogNode is JsonObject changelogObj)
                {
                    FilterStatusChangesOnly(changelogObj);
                }

                foreach (var kvp in jsonObject)
                {
                    OptimizeChangelogs(kvp.Value!);
                }
            }
            else if (node is JsonArray jsonArray)
            {
                foreach (var item in jsonArray)
                {
                    if (item != null) OptimizeChangelogs(item);
                }
            }
        }

        
        /// <summary>
        /// Filters the histories within a changelog to retain only those entries that represent a "status" change.
        /// This method modifies the input <see cref="JsonObject"/> in-place by removing history items that are not status changes,
        /// and subsequently removes entire history entries if they become empty.
        /// </summary>
        /// <param name="changelog">The <see cref="JsonObject"/> representing the changelog. It is expected to have a "histories" property which is a <see cref="JsonArray"/>.</param>
        private static void FilterStatusChangesOnly(JsonObject changelog)
        {
            if (changelog.TryGetPropertyValue("histories", out var historiesNode) && historiesNode is JsonArray histories)
            {
                var historiesToRemove = new List<JsonNode>();

                foreach (var history in histories)
                {
                    if (history is JsonObject historyObj)
                    {
                        if (historyObj.TryGetPropertyValue("items", out var itemsNode) && itemsNode is JsonArray items)
                        {
                            var itemsToRemove = new List<JsonNode>();

                            foreach (var item in items)
                            {
                                if (item is JsonObject itemObj)
                                {
                                    var fieldName = itemObj["field"]?.ToString();
                                    if (string.IsNullOrEmpty(fieldName) || !fieldName.Equals("status", StringComparison.OrdinalIgnoreCase))
                                    {
                                        itemsToRemove.Add(item);
                                    }
                                }
                            }

                            foreach (var item in itemsToRemove) items.Remove(item);

                            if (items.Count == 0)
                            {
                                historiesToRemove.Add(history);
                            }
                        }
                    }
                }

                foreach (var h in historiesToRemove) histories.Remove(h);
            }
        }

        /// <summary>
        /// Recursively cleans a JSON node by removing properties based on predefined rules.
        /// This includes removing properties listed in <see cref="PropertiesToRemove"/>,
        /// null values, empty strings, empty arrays, and fields that contain only template boilerplate.
        /// </summary>
        /// <param name="node">The <see cref="JsonNode"/> to process. The cleaning operation is performed in-place on this node.</param>
        private static void CleanJsonNode(JsonNode node)
        {
            if (node is JsonObject jsonObject)
            {
                var keysToRemove = new List<string>();

                foreach (var kvp in jsonObject)
                {
                    if (PropertiesToRemove.Contains(kvp.Key))
                    {
                        keysToRemove.Add(kvp.Key);
                        continue;
                    }

                    if (kvp.Value == null)
                    {
                        keysToRemove.Add(kvp.Key);
                        continue;
                    }

                    if (kvp.Value is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var strValue))
                    {
                        if (string.IsNullOrWhiteSpace(strValue))
                        {
                            keysToRemove.Add(kvp.Key);
                            continue;
                        }

                        if (TemplateFields.Contains(kvp.Key) && IsEmptyTemplate(strValue))
                        {
                            keysToRemove.Add(kvp.Key);
                            continue;
                        }
                    }

                    if (kvp.Value is JsonArray jsonArray)
                    {
                        if (jsonArray.Count == 0)
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                        else
                        {
                            CleanJsonNode(jsonArray);
                        }
                    }
                    else if (kvp.Value is JsonObject)
                    {
                        CleanJsonNode(kvp.Value);
                    }
                }

                foreach (var key in keysToRemove)
                {
                    jsonObject.Remove(key);
                }
            }
            else if (node is JsonArray array)
            {
                foreach (var item in array)
                {
                    if (item != null)
                    {
                        CleanJsonNode(item);
                    }
                }
            }
        }

        /// <summary>
        /// Checks if a given string consists only of boilerplate template text.
        /// This is used to identify and remove fields that were created from a template but never filled out with meaningful content.
        /// </summary>
        /// <param name="text">The string content to check.</param>
        /// <returns><c>true</c> if the text is considered an empty template (i.e., contains only boilerplate words, panels, and formatting characters); otherwise, <c>false</c>.</returns>
        private static bool IsEmptyTemplate(string text)
        {
            var cleanedText = Regex.Replace(text, @"\{panel.*?\}", "", RegexOptions.IgnoreCase);
            cleanedText = cleanedText.Replace("*", "");

            string[] templateWords =
            [
                "Context:", "DEPENDENCIES", "HOW TO TEST", "EXPECTED OUTPUT", "As", "I would like", "So that",
                "BUSINESS CONTEXT:", "GOAL:", "VALUE:", "REFINEMENT NOTES:", "SCREENS:", "CONTENT:", "BLOCKERS:",
                "Given", "When", "Then", "EXPECTED:", "ACTUAL:"
            ];

            foreach (var word in templateWords)
            {
                cleanedText = Regex.Replace(cleanedText, Regex.Escape(word), "", RegexOptions.IgnoreCase);
            }

            return string.IsNullOrWhiteSpace(cleanedText);
        }
    }
}
