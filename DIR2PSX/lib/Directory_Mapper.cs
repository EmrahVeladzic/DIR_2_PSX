namespace DIR2PSX;

public class DirectoryMapResult
{
    public GameDirectory? Data {get; set;} = null;
    public string? Error {get; set;} = null;
}

public class FileEntryMapResult
{
    public IFileEntry? Data {get; set;} = null;
    public string? Error {get; set;} = null;
}

public static class Directory_Mapper
{    
    public static GameDirectory? Root {get; set;}

    public static Executable? Program {get; set;}

    public static Int32 TotalSize {get; set;} = 0; 

    public static Int32 Level {get; set;} = 0; 

    public static string? Validate()
    {
        if (Root == null)
        {
            return "No root directory found.";
        }
        if (Program == null)
        {
            return "No executable found.";
        }
        if (TotalSize > ISO_Buffer.VOLUME_SIZE)
        {
            return $"Exceeded maximum volume size ({TotalSize}/{ISO_Buffer.VOLUME_SIZE}).";
        }
        
        return Root.Resolve();
    }

    public static FileEntryMapResult MapFile(string path,string name)
    {
        FileEntryMapResult output = new();

        string current = !string.IsNullOrWhiteSpace(name)? Path.Combine(path, name) : path;

        if (!File.Exists(current))
        {
            output.Error=$"{name} - File does not exist.";
            return output;
        }

        FileInfo info = new FileInfo(current);

        if (info.Length >= ISO_Buffer.VOLUME_SIZE)
        {
            output.Error = "Singular file exceeds maximum volume size.";
            return output;
        }

        Int32 file_size = (Int32)info.Length;
        TotalSize+=file_size;

        IFileEntry? file = null;

        if (info.Extension.ToUpperInvariant() == ".ELF")
        {
            if (Program != null)
            {
                output.Error = $"{name} - Multiple ELF files found.";
                return output;
            }


            Executable exe = new();
            exe.Name=name;            
            exe.Size=file_size;
            Program = exe;
            file=exe;
        }
        else if (info.Extension.ToUpperInvariant() == ".EXE")
        {
            output.Error = "You may not use pre-converted .EXE files.";
            return output;
        }
        else
        {
            AssetFile asset = new();
            asset.Name=name;
            asset.IsRaw=false;
            asset.Size=file_size;
            file=asset;
        }

        output.Data = file;
        
        return output;
     
    }

    public static DirectoryMapResult MapDirectory(string path,string name)
    {
        DirectoryMapResult output = new();

        Level++;
        if (Level > 8)
        {
            output.Error="Exceeded the 8th level of subdirectories.";
            return output;
        }

        string current = !string.IsNullOrWhiteSpace(name)? Path.Combine(path, name) : path;

        if (!Directory.Exists(current))
        {
            output.Error=$"{name} - Directory does not exist.";
            return output;
        }

        GameDirectory directory = new();
        directory.Name=name;

        DirectoryInfo info = new DirectoryInfo(current);

        FileInfo[] files = info.GetFiles();
        DirectoryInfo[] subdirectories = info.GetDirectories();
        
        for(int i = 0; i<files.Length; i++)
        {
            FileEntryMapResult file = MapFile(current,files[i].Name);
            if(file.Error!=null||file.Data==null){output.Error=file.Error; return output;}
            else{directory.Files.Add(file.Data);}
        }
        for(int i = 0; i<subdirectories.Length; i++)
        {
            DirectoryMapResult subdirectory = MapDirectory(current,subdirectories[i].Name);
            if(subdirectory.Error!=null||subdirectory.Data==null){output.Error=subdirectory.Error; return output;}
            else{directory.Subdirectories.Add(subdirectory.Data);}
        }

        Level--;
        output.Data=directory;
        return output;
    }

    public static string? Traverse(string path,string name)
    {
        DirectoryMapResult root = MapDirectory(path,name);

        if ((root.Error == null && root.Data == null)||(root.Error!=null && root.Data!=null))
        {
            throw new Exception();
        }

        Root=root.Data;

        if (root.Error != null)
        {
            return root.Error;
        }

        return Validate();

    }

}