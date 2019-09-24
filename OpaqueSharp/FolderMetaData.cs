using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace OpaqueSharp
{
	public class FolderMetaFileVersion
	{
		[JsonProperty("size")]
		public long Size { get; set; }
		[JsonProperty("handle")]
		public string Handle { get; set; }
		[JsonProperty("modified")]
		public long Modified { get; set; }
		[JsonProperty("created")]
		public long Created { get; set; }
	}

	public class FolderMetaFile
	{
		public FolderMetaFile()
		{
			Tags = new List<object>();
			Versions = new List<FolderMetaFileVersion>();
		}
		[JsonProperty("type")]
		public string Type { get; set; }
		[JsonProperty("name")]
		public string Name { get; set; }
		[JsonProperty("created")]
		public long Created { get; set; }
		[JsonProperty("modified")]
		public long Modified { get; set; }
		[JsonProperty("hidden")]
		public bool Hidden { get; set; }
		[JsonProperty("locked")]
		public bool Locked { get; set; }
		[JsonProperty("versions")]
		public List<FolderMetaFileVersion> Versions { get; set; }
		[JsonProperty("tags")]
		public List<object> Tags { get; set; }
	}

	public class FolderMetaData
	{
		public FolderMetaData()
		{
			Files = new List<FolderMetaFile>();
			Tags = new List<string>();
		}
		[JsonProperty("name")]
		public string Name { get; set; }
		[JsonProperty("files")]
		public List<FolderMetaFile> Files { get; set; }
		[JsonProperty("created")]
		public long Created { get; set; }
		[JsonProperty("modified")]
		public long Modified { get; set; }
		[JsonProperty("hidden")]
		public bool Hidden { get; set; }
		[JsonProperty("locked")]
		public bool Locked { get; set; }
		[JsonProperty("tags")]
		public List<string> Tags { get; set; }

		public string Minify(JArray cacheFolderStructure)
		{
			JArray mini = new JArray();
			mini.Add(Name);
			JArray miniFiles = new JArray();
			//Nest root
			mini.Add(miniFiles);

			//Populate files
			foreach (FolderMetaFile file in Files)
			{
				JArray miniFile = new JArray();
				miniFile.Add(file.Name);
				miniFile.Add(file.Created);
				miniFile.Add(file.Modified);
				miniFiles.Add(miniFile);

				JArray miniVersion = new JArray();
				miniFile.Add(new JArray() { miniVersion });

				//Populate versions
				foreach (FolderMetaFileVersion version in file.Versions)
				{
					miniVersion.Add(version.Handle);
					miniVersion.Add(version.Size);
					miniVersion.Add(version.Created);
					miniVersion.Add(version.Modified);
				}
			}

			//Add folders
			mini.Add(cacheFolderStructure);

			//Add Modified and Created
			mini.Add(Created);
			mini.Add(Modified);

			string tst = mini.ToString();
			return mini.ToString(Formatting.None);
		}

		public static FolderMetaData Unminify(JArray minified)
		{
			FolderMetaData fmd = new FolderMetaData();

			fmd.Name = (string)minified[0];
			fmd.Created = (long)minified[3];
			fmd.Modified = (long)minified[4];

			foreach (JArray file in minified[1])
			{
				FolderMetaFile fd = new FolderMetaFile();
				fd.Name = (string)file[0];
				fd.Created = (long)file[1];
				fd.Modified = (long)file[2];
				fd.Versions = new List<FolderMetaFileVersion>();

				foreach (JArray version in file[3])
				{
					FolderMetaFileVersion fv = new FolderMetaFileVersion();
					fv.Handle = (string)version[0];
					fv.Size = (long)version[1];
					fv.Created = (long)version[2];
					fv.Modified = (long)version[3];
					fd.Versions.Add(fv);
				}
				fmd.Files.Add(fd);
			}

			return fmd;
		}
	}
}
