using SideWidget.Infrastructure;
if(args.Length<2)throw new ArgumentException("Usage: manifest|verify|share <application-directory> [output-directory]");
switch(args[0])
{
    case "manifest":DistributionArchive.Prepare(args[1]);DistributionArchive.Validate(args[1]);Console.WriteLine("PASS portable manifest, dependencies and SHA-256");break;
    case "verify":DistributionArchive.Validate(args[1]);Console.WriteLine("PASS portable dependencies and SHA-256");break;
    case "share" when args.Length==3:Console.WriteLine(DistributionArchive.Create(args[1],args[2]));break;
    default:throw new ArgumentException("Unknown distribution command");
}
