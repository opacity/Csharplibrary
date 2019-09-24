using Newtonsoft.Json;
using System.Collections.Generic;

namespace OpaqueSharp
{
	public class FileUploadStatus
	{
		[JsonProperty("status")]
		public string Status { get; set; }
		[JsonProperty("missingIndexes")]
		public List<int> MissingIndexes { get; set; } = new List<int>();
	}
}
