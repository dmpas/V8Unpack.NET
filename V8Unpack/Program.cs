/*----------------------------------------------------------
This Source Code Form is subject to the terms of the 
Mozilla Public License, v.2.0. If a copy of the MPL 
was not distributed with this file, You can obtain one 
at http://mozilla.org/MPL/2.0/.
----------------------------------------------------------*/
using System.Text;

void Usage()
{
    Console.WriteLine("V8Unpack command params");
    Console.WriteLine("Commands:");
    Console.WriteLine("\t-unpack SRC DSTDIR    - one-level unpack with inflate");
    Console.WriteLine("\t-parse  SRC DSTDIR    - recursive unpack");
    Console.WriteLine("\t-build  SRCDIR DST    - recursive pack");
    Console.WriteLine("\t-listfiles SRC        - list file entries");
}

void RecursiveParse(E8Tools.V8Unpack.Container Cf, string destDir, bool showProgress = false)
{
    var progress = 0;

    Directory.CreateDirectory(destDir);
    foreach (var file in Cf.Files())
    {
        var dstPath = Path.Combine(destDir, file.Name);
        var tmpPath = dstPath + ".tmp";

        if (showProgress) Progress(ref progress);

        using var fileStream = file.GetStream();
        using var output = new FileStream(tmpPath, FileMode.Create);
        fileStream.CopyTo(output);
        output.Close();

        using var input = new FileStream(tmpPath, FileMode.Open);
        if (E8Tools.V8Unpack.Container.IsContainer(input))
        {
            var innerCf = E8Tools.V8Unpack.Container.FromStream(input);
            RecursiveParse(innerCf, dstPath);

            input.Close();
            File.Delete(tmpPath);
        }
        else
        {
            input.Close();
            if (File.Exists(dstPath)) {
                File.Delete(dstPath);
            }
            File.Move(tmpPath, dstPath);
        }
    }
}

void Progress(ref int progress)
{
    string[] progressChars = ["|", "/", "-", "\\", "|", "/", "-", "\\"];
    Console.Write($"\b{progressChars[progress]}");
    progress = (progress + 1) % progressChars.Length;
}

void Parse()
{
    if (args.Length < 2)
    {
        Usage();
        return;
    }
    var filename = args[1];
    var destDir = args[2];

    var Cf = E8Tools.V8Unpack.Container.FromFile(filename);
    RecursiveParse(Cf, destDir, true);

    Console.WriteLine("\bDone.");
}

void Unpack()
{
    if (args.Length < 2)
    {
        Usage();
        return;
    }
    
    var filename = args[1];
    var destDir = args[2];
    
    Directory.CreateDirectory(destDir);

    var Cf = E8Tools.V8Unpack.Container.FromFile(filename);
    foreach (var file in Cf.Files())
    {
        var dstPath = Path.Combine(destDir, file.Name);

        using var fileStream = file.GetStream();
        using var output = new FileStream(dstPath, FileMode.Create);

        fileStream.CopyTo(output);

    }
    Console.WriteLine("Done.");
}

void Build()
{
    if (args.Length < 2)
    {
        Usage();
        return;
    }

    var srcDir = args[1];
    var filename = args[2];

    E8Tools.V8Unpack.Container.CreateFromDirectory(srcDir, filename);
    Console.WriteLine("Done.");
}

void ListFiles()
{
    if (args.Length < 1)
    {
        Usage();
        return;
    }

    var filename = args[1];

    var Cf = E8Tools.V8Unpack.Container.FromFile(filename);
    foreach (var file in Cf.Files())
    {
        long fileSize = 0;
        string packedSign = "-";
        string dirSign = "-";
        using (var fileStream = (E8Tools.V8Unpack.BlockReaderStream)file.GetStream(false))
        {
            fileSize = fileStream.Length;
            if (fileStream.IsContainer) dirSign = "d";
            if (fileStream.IsPacked) packedSign = "z";
        }
        
        var namePresentation = new StringBuilder(file.Name);
        for (int i = file.Name.Length; i < 40; i++) namePresentation.Append(" ");

        var sizePresentation = new StringBuilder(fileSize.ToString());

        Console.WriteLine($"{dirSign} {file.ModificationTime.Date:dd.MM.yyyy} {file.ModificationTime.TimeOfDay,8}    {namePresentation}   {fileSize,10} {packedSign} ");

    }
}

if (args ==  null || args.Length < 1)
{
    Usage();
}
else
{
    var command = args[0];
    if (string.Equals(command, "-parse", StringComparison.InvariantCultureIgnoreCase))
    {
        Parse();
    }
    else if (string.Equals(command, "-unpack", StringComparison.InvariantCultureIgnoreCase))
    {
        Unpack();
    }
    else if (string.Equals(command, "-build", StringComparison.InvariantCultureIgnoreCase))
    {
        Build();
    }
    else if (string.Equals(command, "-listfiles", StringComparison.InvariantCultureIgnoreCase))
    {
        ListFiles();
    }
    else
    {
        Usage();
    }
}