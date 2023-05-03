using Microsoft.Extensions.Configuration;
using OpenAI.GPT3;
using OpenAI.GPT3.Managers;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.NamingConventionBinder;
using System.CommandLine.Parsing;


namespace WebCrawlerQnA
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var config = new ConfigurationBuilder()
               .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("env") ?? "dev"}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var apiKey = config.GetSection("ApiKey").Get<string>();

            var openAiService = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = apiKey
            });            


            var buildCommand = new Command("build", "build the QnA")
            {
                new Argument<string>("url", "root url to crawl"),
                new Option<string>(new []{"-p","--path" }, ()=>@"d:\data\", "data path"),
                new Option<int>(new []{"-r","--rpm" }, ()=>20, "request per minute"),
                new Option<int>(new []{"-t","--tpm" }, ()=>150_000, "tokens per minute")
            };


            var askCommand = new Command("ask", "ask questions")
            {
                new Argument<string>("url", "root url to ask"),
                new Option<string>(new []{"-q","--question" },"question"),
                new Option<string>(new []{"-p","--path" }, ()=>@"d:\data\", "data path")                
            };


            buildCommand.Handler = CommandHandler.Create<string, string, int, int>(async (string url, string path, int rpm, int tpm) =>
            {
                var domain = new Uri(url).Host;
                Directory.SetCurrentDirectory(path);
                await WebCrawler.CrawlAsync(url);
                TextProcessor.ProcessTextFiles(domain);
                var df = TextTokenizer.TokenizeTextFile(domain);
                await TextEmbedding.CreateEmbeddings(openAiService, df, domain, rpm, tpm);
            });

            askCommand.Handler = CommandHandler.Create<string,string,string>(async (string url, string path, string question) =>
            {
                var domain = new Uri(url).Host;
                Directory.SetCurrentDirectory(path);
                var answer = await TextEmbedding.AnswerQuestion(openAiService, domain, question);
                if (!string.IsNullOrEmpty(answer))
                {
                    Console.WriteLine(answer); 
                }
            });

            var rootCommand = new RootCommand("A simple Q&A CLI application.")
            {
                buildCommand,
                askCommand
            };

            var builder = new CommandLineBuilder(rootCommand);
            builder.UseDefaults();
            builder.UseHelp();
            builder.UseVersionOption();
            var parser = builder.Build();

            await parser.InvokeAsync(args);
        }
    }
}