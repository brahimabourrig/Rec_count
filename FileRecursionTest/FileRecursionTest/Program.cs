using System;
using System.IO;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;

namespace FileRecursionTest
{
	static class Program
	{
		static long totalSize = 0;
		static long totalCount = 0;

		static void Main(string[] args)
		{
			if (args.Length == 0)
			{
				Console.Error.WriteLine($"Need a directory path to recurse.");
			}
			DirectoryInfo system = new(args[0]);
			Console.WriteLine($"Beginning Enumeration of {system.FullName}");
			DateTime start = DateTime.Now;
			EnumerateFileDirEntries(system);
			var duration = DateTime.Now - start;
			Console.WriteLine();
			Console.WriteLine($"Took {duration} for {totalCount} files with a size of {totalSize} bytes");
		}

		private static void EnumerateFileDirEntries(DirectoryInfo cwd, DirectoryInfo baseDir = null)
		{
			using Timer displayTimer = new Timer((x) => {
				Console.Write($"\r{Interlocked.Read(ref totalSize)}");
			}, null, 500, 500);
			try
			{
				IEnumerable<FileInfo> files = cwd.EnumerateFiles("*.*", SearchOption.TopDirectoryOnly);
				Interlocked.Add(ref totalCount, files.LongCount());
				
				Parallel.ForEach(files, (x) => {
					Interlocked.Add(ref totalSize, x.Length);
				});

				using (CancellationTokenSource cancelSignal = new CancellationTokenSource())
				{
					try
					{
						ParallelOptions options = new ParallelOptions
						{
							CancellationToken = cancelSignal.Token
						};
						Parallel.ForEach(
							cwd.EnumerateDirectories("*.*", SearchOption.TopDirectoryOnly),
							options,
							(dirInfo, state) =>
							{
								try
								{
									if (state.ShouldExitCurrentIteration)
									{
										Console.WriteLine("Loop state exit flag is true. Returning without processing {0}", dirInfo.FullName);
									}
									if ((dirInfo.Attributes & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint)
										EnumerateFileDirEntries(dirInfo, baseDir);
								}
								catch (OperationCanceledException oce)
								{
									Console.Error.WriteLine($"Enumeration canceled: {oce.Message}");
									state.Stop();
								}
							});
					}
					catch (Exception ex)
					{
						cancelSignal.Cancel();
						Console.WriteLine($"Error while enumerating {cwd.FullName} ec:{ex.HResult}-{ex.Message}");
						throw;
					}
				}
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(e.Message);
				throw;
			}
		}
	}
}
