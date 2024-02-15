using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Agent.Tests
{
    public static class Utilities
    {
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

                Console.WriteLine($"Temporary file '{filePath}' created successfully with size {fileSizeInBytes} bytes.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating the temporary file: {ex.Message}");
            }
        }
    }
}
