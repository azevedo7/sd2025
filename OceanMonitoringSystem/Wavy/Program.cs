// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello from wavy!");

static void Main(string[] args)
{
    int wavyId = 0;
    string aggregatorIp = "127.0.0.1";
    int aggregatorPort = 8001;

    if(args.Length >= 1) wavyId = int.Parse(args[0]);
    if (args.Length >= 2) aggregatorIp = args[1];
    if (args.Length >= 3) aggregatorPort = int.Parse(args[2]);

    Console.WriteLine("teste");
}

Main(args);