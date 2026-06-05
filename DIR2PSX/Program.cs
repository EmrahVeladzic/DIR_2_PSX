namespace DIR2PSX;

class Program
{
    static int Main(string[] args)
    {
        try
        {           
        
            string? sub_dir = args.Length > 0 ? args[0] : null;

            if (string.IsNullOrWhiteSpace(sub_dir))
            {
                Console.WriteLine("Please provide the name of the subdirectory you wish to convert.");
                return 0;
            }

            string current_dir = Directory.GetCurrentDirectory();

            string full_dir = Path.Combine(current_dir, sub_dir);


            if (!Directory.Exists(full_dir))
            {
                Console.WriteLine($"Directory '{sub_dir}' not found.");
                return 1;
            }

            string? mapping_output = Directory_Mapper.Traverse(current_dir,sub_dir??"");
            if (mapping_output != null)
            {
                Console.WriteLine(mapping_output);
                return 1;
            }

            return 0;
            
        }
        catch
        {
            Console.WriteLine("An unexpected error has occurred.");

            return 1;
        }        
        
    }
}