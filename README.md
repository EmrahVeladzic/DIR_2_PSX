.NET PlayStation .bin file creator. Generates a bootable disc image. Requires .NET 8.

Usage:
1. Publish the application with "dotnet publish -c Release" and move the resulting 
   executable from the publish output folder to the target folder's parent, or add it to PATH.
2. Place a compiled .elf file in the target directory, alongside any asset files.
   Ensure the directory structure matches what the program expects.
3. Run the application, passing the target directory as an argument:
   <appname> <path/to/directory>
4. Follow any on-screen instructions if issues arise during conversion.

The resulting .bin file contains the executable and assets.