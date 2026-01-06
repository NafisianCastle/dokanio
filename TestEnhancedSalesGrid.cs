using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Shared.Core.Tests;

namespace TestSalesGrid
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Enhanced Sales Grid Engine Test Runner");
            Console.WriteLine("=====================================");
            
            try
            {
                var tests = new EnhancedSalesGridEngineTests();
                var success = await tests.RunAllTests();
                
                if (success)
                {
                    Console.WriteLine("\n🎉 All tests passed!");
                    Environment.Exit(0);
                }
                else
                {
                    Console.WriteLine("\n❌ Some tests failed!");
                    Environment.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n💥 Test execution failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                Environment.Exit(1);
            }
            finally
            {
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
            }
        }
    }
}