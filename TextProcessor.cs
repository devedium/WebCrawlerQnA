using Microsoft.Data.Analysis;
using System.Data;
using System.Text.RegularExpressions;

namespace WebCrawlerQnA
{
    public class TextProcessor
    {
        private static string RemoveNewlines(string input)
        {
            string result = Regex.Replace(input, @"\n", " ");
            result = Regex.Replace(result, @"\\n", " ");
            result = Regex.Replace(result, "\\s+", " ");            
            return result;
        }
        
        public static void ProcessTextFiles(string domain)
        {
            string textDirectoryPath = $"text/{domain}";

            int prefixLen = domain.Length + 1;
            List<Tuple<int, string, string>> texts = new List<Tuple<int, string, string>>();

            int index = 0;
            foreach (var file in Directory.GetFiles(textDirectoryPath))
            {
                using (StreamReader reader = new StreamReader(file, System.Text.Encoding.UTF8))
                {
                    string text = reader.ReadToEnd();
                    string fileName = Path.GetFileName(file);
                    string formattedName = fileName.Substring(prefixLen, fileName.Length >= prefixLen + 4 ? fileName.Length - prefixLen - 4 : 0).Replace('-', ' ').Replace('_', ' ').Replace("#update", "");
                    texts.Add(new Tuple<int, string, string>(index++, formattedName, $"{formattedName}.{RemoveNewlines(text)}"));
                }
            }

            var dataFrame = new DataFrame(new List<DataFrameColumn>
            {
                new PrimitiveDataFrameColumn<int>("id"),
                new StringDataFrameColumn("fname"),
                new StringDataFrameColumn("text")
            });

            foreach (var row in texts.Select(x => new Dictionary<string, object> { { "id", x.Item1 }, { "fname", x.Item2 }, { "text", x.Item3 } }))
            {
                dataFrame.Append(row, true);
            }

            // Create a directory to store the csv files            
            string csvDirectoryPath = $"processed/{domain}";
            Directory.CreateDirectory(csvDirectoryPath);

            DataFrame.SaveCsv(dataFrame, $"{csvDirectoryPath}/scraped.csv");
        }
    }
}
