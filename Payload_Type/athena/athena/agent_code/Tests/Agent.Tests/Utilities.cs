using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests
{
    public static class Utilities
    {
        public static string CreateTempDirectoryWithRandomFiles()
        {
            int fileCount = 6;
            // Create a temporary directory
            string tempDirectoryPath = Path.Combine(Path.GetTempPath(), "RandomFiles_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDirectoryPath);

            // Random number generator for file content
            Random random = new Random();

            for (int i = 0; i < fileCount; i++)
            {
                // Generate a random file name and content length
                string filePath = Path.Combine(tempDirectoryPath, $"RandomFile_{i + 1}.txt");
                int contentLength = random.Next(100, 500); // random data size between 100 and 500 bytes

                // Generate random content for the file
                byte[] fileContent = new byte[contentLength];
                random.NextBytes(fileContent);

                // Write the random content to the file
                File.WriteAllBytes(filePath, fileContent);
            }

            return tempDirectoryPath;
        }
        public static bool CreateZipFile(string zipFilePath)
        {
            // Define the number of files and their random data size range
            int numberOfFiles = 6;
            Random random = new Random();
            try
            {
                // Create the zip archive
                using (FileStream zipToOpen = new FileStream(zipFilePath, FileMode.Create))
                using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                {
                    for (int i = 0; i < numberOfFiles; i++)
                    {
                        // Generate a random file name and content length
                        string fileName = $"RandomFile_{i + 1}.txt";
                        int contentLength = random.Next(100, 500); // random data size between 100 and 500 bytes

                        // Generate random content for the file
                        byte[] fileContent = new byte[contentLength];
                        random.NextBytes(fileContent);

                        // Add the file to the zip archive
                        ZipArchiveEntry entry = archive.CreateEntry(fileName);
                        using (Stream entryStream = entry.Open())
                        {
                            entryStream.Write(fileContent, 0, fileContent.Length);
                        }
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }
        public static string GenerateRandomText(long sizeInBytes)
        {
            // Define characters to be used for random text
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

            // Create a random number generator
            Random random = new Random();

            // Generate random text of the specified size
            char[] randomText = new char[sizeInBytes];
            for (int i = 0; i < sizeInBytes; i++)
            {
                randomText[i] = chars[random.Next(chars.Length)];
            }

            return new string(randomText);
        }
        public static void CreateTemporaryFileWithRandomText(string filePath, long fileSizeInBytes)
        {
            try
            {
                // Generate random text
                string randomText = GenerateRandomText(fileSizeInBytes);

                // Write the random text to the temporary file
                File.WriteAllText(filePath, randomText);
                Console.WriteLine(randomText);

                Console.WriteLine($"Temporary file '{filePath}' created successfully with size {fileSizeInBytes} bytes.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating the temporary file: {ex.Message}");
            }
        }
    }
}
