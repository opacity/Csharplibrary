using Newtonsoft.Json;

namespace OpaqueSharp
{
	public class FileMetaOptions
	{
		[JsonProperty("blockSize")]
		public int BlockSize { get; set; }
		[JsonProperty("partSize")]
		public long PartSize { get; set; }
	}

	public class FileMetaData
	{
		[JsonProperty("name")]
		public string Name { get; set; }
		[JsonProperty("type")]
		public string Type { get; set; }
		[JsonProperty("size")]
		public long Size { get; set; }
		[JsonProperty("p")]
		public FileMetaOptions P { get; set; }

		public static FileMetaData CreateMetaData(FileData file, FileMetaOptions opts)
		{
			return new FileMetaData
			{
				Name = file.Name,
				Type = file.Type,
				Size = file.Size,
				P = opts
			};
		}

	}
}
