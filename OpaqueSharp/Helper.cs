using NBitcoin;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace OpaqueSharp
{
	public class Helper
	{
		public static string BlobToString(byte[] blob)
		{
			return Encoding.UTF8.GetString(blob).TrimEnd("\r\n\0".ToCharArray());
		}

		public static byte[] GenerateFileKeys()
		{
			RNGCryptoServiceProvider provider = new RNGCryptoServiceProvider();
			byte[] handle = new byte[64];
			provider.GetBytes(handle);
			return handle;
		}

		public static FileData GetFileData(FileInfo file)
		{
			return new FileData()
			{
				//Data = readFileInfoBytes(file),
				Name = file.Name,
				Size = file.Length,
				Type = getMymeType(file.Name)
			};
		}

		public static byte[] GetPartial(FileInfo info, long partSize, int index)
		{
			lock (info)
			{
				long remaining = (info.Length - (long)index * (long)partSize);
				int takeSize = (int)Math.Min(remaining, (long)partSize);

				byte[] part = new byte[partSize];
				using (FileStream fs = new FileStream(info.FullName, FileMode.Open))
				{
					fs.Seek(partSize * (long)index, SeekOrigin.Begin);
					byte[] read = fs.ReadBytes((int)takeSize);
					//File.WriteAllBytes(info.FullName + "." + index, read);
					return read;
				}
			}
		}

		public static long GetUploadSize(long size)
		{
			int blockSize = Constants.DEFAULT_BLOCK_SIZE;
			//int blockCount = (int)(size / blockSize) + 1;
			int blockCount = (int)Math.Ceiling((double)size / (double)blockSize);
			return size + blockCount * Constants.BLOCK_OVERHEAD;
		}

		public static int GetEndIndex(long uploadSize, FileMetaOptions p)
		{
			int blockSize = p.BlockSize;
			long partSize = p.PartSize;
			int chunkSize = blockSize + Constants.BLOCK_OVERHEAD;
			int chunkCount = (int)(uploadSize / chunkSize) + 1;
			int chunksPerPart = (int)(partSize / chunkSize) + 1;
			//int chunkCount = (int)Math.Ceiling((double)uploadSize / (double)chunkSize);
			//int chunksPerPart = (int)Math.Ceiling((double)partSize / (double)chunkSize);
			int endIndex = (chunkCount / chunksPerPart) + 1;
			//int endIndex = (int)Math.Ceiling((double)chunkSize / (double)chunksPerPart);

			return endIndex;
		}

		public static ExtKey Wallet()
		{
			//https://programmingblockchain.gitbook.io/programmingblockchain/key_generation/bip_32
			string accountHandle = "92feabd33f10079ba966c219c072a28c3e466f140db4b68c2d2dd8301c7533abc85e9b6671716b331786b08edb1203cbaa972c35e3b3af6e71af58082b5cc4a2";
			string privateKey = accountHandle.Substring(0, 64);
			string chaincode = accountHandle.Substring(64, 64);

			byte[] privateKeyBytes = AesGcm256.HexToByte(privateKey);
			byte[] chainCodeBytes = AesGcm256.HexToByte(chaincode);
			Key key = new Key();
			key.FromBytes(privateKeyBytes);
			return new ExtKey(key, chainCodeBytes);

		}

		/*
		 >>>	"aabb00ff22ff995588aa"
				.match(/.{ 1,4}/g)  // actual: [0-9a-f]{4}
				.map(p => parseInt(p, 16))
				.join("'/") + "'"
		<<<		"43707'/255'/8959'/39253'/34986'"
		(prefix ? "m/" : "") + h.match(/.{ 1,4}/ g).map(p => parseInt(p, 16)).join("'/") + "'";
		43707'/255'/8959'/39253'/34986'
		43707'/255'/8959'/39253'/34986'
		*/

		private static string hashToPath(string h, bool prefix = false)
		{
			if (h.Length % 4 == 1)
			{
				throw new Exception("hash length must be multiple of two bytes");
			}

			//h = "aabb00ff22ff995588aa";
			List<string> groups = (from Match m in Regex.Matches(h, @"[0-9a-f]{4}") select m.Value).ToList();
			List<int> numberGroups = groups.ConvertAll<int>(o => int.Parse(o, System.Globalization.NumberStyles.HexNumber));
			string res = (prefix ? "m/" : "") + string.Join("'/", numberGroups) + "'";
			return res;
		}

		private static ExtKey generateSubHDKey(ExtKey key, string pathString)
		{
			string hash = new Sha3Keccack().CalculateHash(pathString);
			string path = hashToPath(hash, true);
			KeyPath keyPath = new KeyPath(path);

			return key.Derive(keyPath);
		}

		public static ExtKey getFileHDKey(ExtKey key, string file)
		{
			return generateSubHDKey(key, "file: " + file);
		}

		public static ExtKey getFolderHDKey(ExtKey key, string dir)
		{
			return generateSubHDKey(key, "folder: " + dir);
		}

		public static string getFolderLocation(ExtKey key, string dir)
		{
			return Sha3Keccack.Current.CalculateHash(getFolderHDKey(key, dir).Neuter().PubKey.ToHex());
		}

		public static void getFolderMeta(ExtKey key, string dir)
		{
			ExtKey folderKey = getFolderHDKey(key, dir);
			string location = getFolderLocation(key, dir);
			string keyString = Sha3Keccack.Current.CalculateHash(folderKey.PrivateKey.ToString(Network.Main));
		}

		/*			const timestamp = Math.floor(Date.now() / 1000);
			const payload = { timestamp, metadataKey };
			const signedPayload = getPayload(payload, hdNode);

			RestClient client = new RestClient("https://s3.us-east-2.amazonaws.com/opacity-prod/" + fileid + "/file");
			request = new RestRequest("", DataFormat.Json);
			response = client.Get(request);

			Axios.post(endpoint + "/api/v1/metadata/get", signedPayload);
			*/

		public static async Task<string> PostFormData(MultipartFormDataContent payload, string url)
		{
			try
			{
				HttpClient httpClient = new HttpClient();
				httpClient.Timeout = new TimeSpan(0, 10, 0);

				HttpResponseMessage response = await httpClient.PostAsync(url, payload);
				if (!response.IsSuccessStatusCode)
					throw new Exception();
				//response.EnsureSuccessStatusCode();
				httpClient.Dispose();
				string sd = response.Content.ReadAsStringAsync().Result;
				//Console.WriteLine(sd);
				return sd;
			}
			catch (Exception e)
			{
				Console.WriteLine("Upload error " + e.Message);
				throw e;
			}

		}

		public static long GetUnixMilliseconds()
		{
			DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
			DateTime utcNow = DateTime.UtcNow;
			TimeSpan elapsedTime = utcNow - unixEpoch;
			return (long)elapsedTime.TotalMilliseconds;
		}

		private static string getMymeType(string fileName)
		{
			return MimeMapping.GetMimeMapping(fileName);
		}

		private static byte[] readFileInfoBytes(FileInfo file)
		{
			return File.ReadAllBytes(file.FullName);
		}
		public static bool CheckHex(string test)
		{
			// For C-style hex notation (0xFF) you can use @"\A\b(0[xX])?[0-9a-fA-F]+\b\Z"
			return System.Text.RegularExpressions.Regex.IsMatch(test, @"\A\b[0-9a-fA-F]+\b\Z");
		}
	}
}
