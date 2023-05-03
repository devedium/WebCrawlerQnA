using AI.Dev.OpenAI.GPT;
using Microsoft.Data.Analysis;

namespace WebCrawlerQnA
{
    public class TextTokenizer
    {
        public static DataFrame CreateDataFrameWithNTokens(string filePath)
        {
            // Load the CSV file
            var df = DataFrame.LoadCsv(filePath);

            // Rename columns
            df.Columns[0].SetName("index");
            df.Columns[1].SetName("title");
            df.Columns[2].SetName("text");            

            var tokenCounts = df.Columns["text"].Cast<string>().Select(t => GPT3Tokenizer.Encode(t).Count).ToArray();

            // Add the token counts to the DataFrame
            var tokenCountsColumn = new PrimitiveDataFrameColumn<int>("n_tokens", tokenCounts);

            df.Columns.Add(tokenCountsColumn);           
            
            return df;
        }

        // Function to split the text into chunks of a maximum number of tokens
        private static List<string> SplitIntoMany(string text, int maxTokens)
        {
            // Split the text into sentences
            var sentences = text.Split(". ").Where(s=>!string.IsNullOrEmpty(s)).ToArray();            

            var tokenCounts = sentences.Select(t => GPT3Tokenizer.Encode(t).Count).ToArray();

            // Add the token counts to the DataFrame
            var tokenCountsColumn = new PrimitiveDataFrameColumn<int>("n_tokens", tokenCounts);

            var chunks = new List<string>();
            int tokensSoFar = 0;
            var chunk = new List<string>();

            // Loop through the sentences and tokens joined together in a tuple
            for (int i = 0; i < sentences.Length; i++)
            {
                var sentence = sentences[i];
                var token = tokenCounts[i];

                // If the number of tokens so far plus the number of tokens in the current sentence is greater 
                // than the max number of tokens, then add the chunk to the list of chunks and reset
                // the chunk and tokens so far
                if (tokensSoFar + token > maxTokens)
                {
                    chunks.Add(string.Join(". ", chunk) + ".");
                    chunk = new List<string>();
                    tokensSoFar = 0;
                }

                // If the number of tokens in the current sentence is greater than the max number of 
                // tokens, go to the next sentence
                if (token > maxTokens)
                {
                    continue;
                }

                // Otherwise, add the sentence to the chunk and add the number of tokens to the total
                chunk.Add(sentence);
                tokensSoFar += token + 1;
            }

            // Add the last chunk to the list of chunks
            if (chunk.Count > 0)
            {
                chunks.Add(string.Join(". ", chunk) + ".");
            }

            return chunks;
        }

        private static List<string> SplitTextIntoChunks(DataFrame dataFrame)
        {
            int maxTokens = 500;                       

            var shortened = new List<string>();

            // Loop through the DataFrame
            for (long rowIndex = 0; rowIndex < dataFrame.Rows.Count; rowIndex++)
            {
                var row = dataFrame.Rows[rowIndex];
                var text = row[dataFrame.Columns.IndexOf("text")].ToString();

                // If the text is null, go to the next row
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                // If the number of tokens is greater than the max number of tokens, split the text into chunks
                int nTokens = (int)row[dataFrame.Columns.IndexOf("n_tokens")];
                if (nTokens > maxTokens)
                {
                    shortened.AddRange(SplitIntoMany(text, maxTokens));
                }
                // Otherwise, add the text to the list of shortened texts
                else
                {
                    shortened.Add(text);
                }
            }

            return shortened;
        }

        private static DataFrame CreateShortenedDataFrame(List<string> shortenedTexts)
        {
            // Create a DataFrame with the shortened texts
            var df = new DataFrame(new StringDataFrameColumn("text", shortenedTexts));            

            var tokenCounts = df.Columns["text"].Cast<string>().Select(t => GPT3Tokenizer.Encode(t).Count).ToArray();

            // Add the token counts to the DataFrame
            var tokenCountsColumn = new PrimitiveDataFrameColumn<int>("n_tokens", tokenCounts);
            df.Columns.Add(tokenCountsColumn);          

            return df;
        }

        public static DataFrame TokenizeTextFile(string domain)
        {
            var filePath = $"processed/{domain}/scraped.csv";

            var df = CreateDataFrameWithNTokens(filePath);

            var chunks = SplitTextIntoChunks(df);

            var sdf = CreateShortenedDataFrame(chunks);

            return sdf;
        }
    }
}
