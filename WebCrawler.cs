using HtmlAgilityPack;
using System.Net;
using System.Text.RegularExpressions;

namespace WebCrawlerQnA
{
    public class WebCrawler
    {
        // Define a regex pattern to match a URL
        private static readonly Regex HttpUrlPattern = new(@"^http[s]*://.+", RegexOptions.Compiled);

        // Function to get the hyperlinks from a URL
        public static async Task<List<string>> GetHyperlinksAsync(string url)
        {
            try
            {
                using var httpClient = new HttpClient();
                using var httpResponse = await httpClient.GetAsync(url);

                if (httpResponse.Content?.Headers?.ContentType?.MediaType?.StartsWith("text/html") == false)
                {
                    return new List<string>();
                }

                using var contentStream = await httpResponse!.Content!.ReadAsStreamAsync();
                using var reader = new StreamReader(contentStream);
                var html = await reader.ReadToEndAsync();               

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var hyperlinks = new List<string>();

                foreach (var linkNode in (doc.DocumentNode.SelectNodes("//a[@href]"))??Enumerable.Empty<HtmlNode>())
                {
                    var href = linkNode.GetAttributeValue("href", string.Empty);
                    if (!string.IsNullOrEmpty(href))
                    {
                        hyperlinks.Add(href);
                    }
                }

                return hyperlinks;
            }
            catch (Exception e) 
            {
                Console.WriteLine(e);
                return new List<string>();
            }
        }

        // Function to get the hyperlinks from a URL that are within the same domain
        public static async Task<List<string>> GetDomainHyperlinksAsync(string domain, string url)
        {
            var cleanLinks = new HashSet<string>();
            var rawLinks = await GetHyperlinksAsync(url);
            var uri = new Uri(url);

            foreach (var link in rawLinks.Distinct())
            {
                string? cleanLink = null;

                // If the link is a URL, check if it is within the same domain
                if (HttpUrlPattern.IsMatch(link))
                {
                    // Parse the URL and check if the domain is the same
                    var linkUri = new Uri(link);
                    if (linkUri.Host == domain)
                    {
                        cleanLink = link;
                    }
                }
                // If the link is not a URL, check if it is a relative link
                else
                {
                    if (link.StartsWith("/"))
                    {
                        cleanLink = $"{uri.Scheme}://{domain}/{link.Substring(1)}";
                    }
                    else if (link.StartsWith("#") || link.StartsWith("mailto:"))
                    {
                        continue;
                    }
                    else
                    {
                        Uri absoluteUri = new Uri(uri, link);
                        cleanLink = absoluteUri.ToString();
                    }
                    
                }

                if (cleanLink != null)
                {
                    if (cleanLink.EndsWith("/"))
                    {
                        cleanLink = cleanLink.Remove(cleanLink.Length - 1);
                    }
                    cleanLinks.Add(cleanLink);
                }
            }

            // Return the list of hyperlinks that are within the same domain
            return cleanLinks.ToList();
        }

        public static async Task CrawlAsync(string url)
        {
            // Parse the URL and get the domain
            var uri = new Uri(url);
            var domain = uri.Host;

            // Create a queue to store the URLs to crawl
            var queue = new Queue<string>();
            queue.Enqueue(url);

            // Create a set to store the URLs that have already been seen (no duplicates)
            var seen = new HashSet<string> { url };

            // Create a directory to store the text files
            var textDirectoryPath = $"text/{domain}";
            Directory.CreateDirectory(textDirectoryPath);            

            // While the queue is not empty, continue crawling
            while (queue.Count > 0)
            {
                // Get the next URL from the queue
                url = queue.Dequeue();
                Console.WriteLine(url); // for debugging and to see the progress

                // Save text from the url to a <url>.txt file
                string invalidChars = new string(Path.GetInvalidFileNameChars());
                var validFileName = new string(url.Substring(uri.Scheme.Length + 3).Select(ch => invalidChars.Contains(ch) ? '_' : ch).ToArray());
                var fileName = $"{textDirectoryPath}/{validFileName}.txt";
                
                // Get the text from the URL using HtmlAgilityPack
                var web = new HtmlWeb();
                HttpStatusCode statusCode = HttpStatusCode.NoContent;
                string? contentType = null;
                web.PostResponse = (req, res) => { statusCode = res.StatusCode; contentType = res.ContentType; };
                var doc = web.Load(url);

                if (statusCode == HttpStatusCode.OK && contentType!= null && contentType.Contains("text/html"))
                {
                    using (var writer = new StreamWriter(fileName, false, System.Text.Encoding.UTF8))
                    {
                        // Get the text but remove the tags
                        var text = doc.DocumentNode.InnerText;

                        // If the crawler gets to a page that requires JavaScript, it will stop the crawl
                        if (text.Contains("You need to enable JavaScript to run this app."))
                        {
                            Console.WriteLine($"Unable to parse page {url} due to JavaScript being required");
                        }

                        // Otherwise, write the text to the file in the text directory
                        await writer.WriteAsync(text);
                    }

                    // Get the hyperlinks from the URL and add them to the queue
                    var hyperlinks = await GetDomainHyperlinksAsync(domain, url);
                    foreach (var link in hyperlinks)
                    {
                        if (!seen.Contains(link))
                        {
                            queue.Enqueue(link);
                            seen.Add(link);
                        }
                    }
                }                
            }
        }
    }
}
