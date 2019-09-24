using NBitcoin;
using Nethereum.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace OpaqueSharp
{
	public class Account
	{
		public PaymentStatus Status;
		private ExtKey masterKey;
		private string privateKey;
		private string chainCode;

		//TODO: REMOVE TEMP WHEN FOLDERS IMPLEMENTED
		private JArray cacheFolderStructure;

		public Account(string accountHandle)
		{
			if (accountHandle.Length != 128 || !Helper.CheckHex(accountHandle))
				throw new InvalidDataException("Account Handle not valid.");

			//https://programmingblockchain.gitbook.io/programmingblockchain/key_generation/bip_32
			privateKey = accountHandle.Substring(0, 64);
			chainCode = accountHandle.Substring(64, 64);

			byte[] privateKeyBytes = AesGcm256.HexToByte(privateKey);
			byte[] chainCodeBytes = AesGcm256.HexToByte(chainCode);
			Key key = new Key();
			key.FromBytes(privateKeyBytes);
			masterKey = new ExtKey(key, chainCodeBytes);

			//Check Account
			try
			{
				Status = CheckAccountStatus();
			}
			catch
			{
				Status = null;
			}
		}

		List<Tuple<Dictionary<string, object>, string, string>> accStatusCache = new List<Tuple<Dictionary<string, object>, string, string>>();

		public PaymentStatus CheckAccountStatus()
		{
			//Check Account
			Dictionary<string, object> requestBody = new Dictionary<string, object>();
			var timestamp = Helper.GetUnixMilliseconds();
			requestBody.Add("timestamp", timestamp);
			string requestBodyJSON = JsonConvert.SerializeObject(requestBody);
			var payloadUpdateStatus = Crypto.SignPayloadDict(requestBodyJSON, "requestBody", new Dictionary<string, ByteArrayContent>
			{
			}, privateKey);


			string payloadUpdateStatusJSON = JsonConvert.SerializeObject(payloadUpdateStatus);

			RestClient client = new RestClient("https://broker-1.opacitynodes.com:3000/api/v1/account-data");
			RestRequest request = new RestRequest("", Method.POST, DataFormat.Json);
			request.AddJsonBody(payloadUpdateStatusJSON);
			var response = client.Post(request);

			var cacheEntry = new Tuple<Dictionary<string, object>, string, string>(requestBody, payloadUpdateStatusJSON, response.Content);
			accStatusCache.Add(cacheEntry);

			string metaDataUpdate = response.Content;
			return JsonConvert.DeserializeObject<PaymentStatus>(metaDataUpdate);
		}

		public FolderMetaData GetFolderMetaData(string folder)
		{
			try
			{
				return getFolderMetaRequest(folder);
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				throw;
			}
		}

		private FolderMetaData getFolderMetaRequest(string folder)
		{
			ExtKey folderKey = Helper.getFolderHDKey(masterKey, folder);

			string location = Helper.getFolderLocation(masterKey, folder); //metaDataKey
			string fdPrivate = AesGcm256.toHex(folderKey.PrivateKey.ToBytes()).ToLower();
			string keyString = Sha3Keccack.Current.CalculateHash(fdPrivate);

			//GET FOLDER METADATA
			var timestamp = Helper.GetUnixMilliseconds();
			Dictionary<string, object> metaReqDict = new Dictionary<string, object>
			{
				{"timestamp", timestamp},
				{"metadataKey", location }
			};
			var payloadMetaJSON = JsonConvert.SerializeObject(metaReqDict);
			var payloadMeta = Crypto.SignPayloadDict(payloadMetaJSON, "requestBody", new Dictionary<string, ByteArrayContent>
			{
			}, privateKey);

			string json = JsonConvert.SerializeObject(payloadMeta);

			RestClient client = new RestClient("https://broker-1.opacitynodes.com:3000/api/v1/metadata/get");
			var request = new RestRequest("", Method.POST, DataFormat.Json);
			request.AddJsonBody(payloadMeta);
			var response = client.Post(request);

			//Result metadata 1.0
			string resultMetaDataEncrypted = response.Content;
			dynamic resultMetaEncryptedDataPack = JsonConvert.DeserializeObject(resultMetaDataEncrypted);
			try
			{
				byte[] resultMetaDataEncryptedBytes = AesGcm256.HexToByte((string)resultMetaEncryptedDataPack["metadata"]);

				byte[] decryptedMetaData = AesGcm256.decrypt(resultMetaDataEncryptedBytes, AesGcm256.HexToByte(keyString));
				string resultMetaData = Helper.BlobToString(decryptedMetaData);

				return JsonConvert.DeserializeObject<FolderMetaData>(resultMetaData);
			}

			//Result metadata 2.0
			catch
			{
				try
				{
					byte[] data = System.Convert.FromBase64String((string)resultMetaEncryptedDataPack["metadata"]);

					byte[] decryptedMetaData = AesGcm256.decrypt(data, AesGcm256.HexToByte(keyString));
					string resultMetaData = Helper.BlobToString(decryptedMetaData);

					JArray jData = JsonConvert.DeserializeObject<JArray>(resultMetaData);
					cacheFolderStructure = jData[2] as JArray;
					FolderMetaData fdMeta = FolderMetaData.Unminify(jData);
					return fdMeta;
				}
				catch (Exception e)
				{
					//Both 1.0 and 2.0 failed this is not a valid handle
					throw new Exception("Account handle invalid, verify the input if it still fails please contact support at telegram https://t.me/OpacityStorage.", e);
				}
			}
		}

		public void AddFileToFolderMetaData(string folder, FolderMetaFile file)
		{
			//return;
			try
			{
				//Get folderkey
				ExtKey folderKey = Helper.getFolderHDKey(masterKey, folder);
				string location = Helper.getFolderLocation(masterKey, folder); //metaDataKey
				string fdPrivate = AesGcm256.toHex(folderKey.PrivateKey.ToBytes()).ToLower();
				string keyString = Sha3Keccack.Current.CalculateHash(fdPrivate);

				//Get latest meta and add new file
				FolderMetaData metaData = GetFolderMetaData(folder);
				metaData.Files.Add(file);

				//Clean out bug deleted files
				metaData.Files.RemoveAll(o => o.Versions.Any() == false);

				string metaDataMinifiedJSON = metaData.Minify(cacheFolderStructure);

				//Serialize and encrypt
				//string metaDataJSON = JsonConvert.SerializeObject(metaData);
				byte[] encryptedMetaData = AesGcm256.encryptString(metaDataMinifiedJSON, AesGcm256.HexToByte(keyString));
				//string encryptedMetaDataHex = AesGcm256.toHex(encryptedMetaData).ToLower();
				string encryptedMetaDataBase64 = System.Convert.ToBase64String(encryptedMetaData);

				//SET FOLDER METADATA
				var timestamp = Helper.GetUnixMilliseconds();
				Dictionary<string, object> metaReqDict = new Dictionary<string, object>
			{
				{"timestamp", timestamp},
				{"metadataKey", location },
				{"metadata", encryptedMetaDataBase64 }
			};
				var payloadMetaJSON = JsonConvert.SerializeObject(metaReqDict);
				var payloadMeta = Crypto.SignPayloadDict(payloadMetaJSON, "requestBody", new Dictionary<string, ByteArrayContent>
				{
				}, privateKey);

				string json = JsonConvert.SerializeObject(payloadMeta);

				RestClient client = new RestClient("https://broker-1.opacitynodes.com:3000/api/v1/metadata/set");
				var request = new RestRequest("", Method.POST, DataFormat.Json);
				request.AddJsonBody(payloadMeta);
				var response = client.Post(request);
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				throw;
			}
		}

		public void RenameFile(string folder, string oldName, string newName)
		{
			//return;
			try
			{
				//Get folderkey
				ExtKey folderKey = Helper.getFolderHDKey(masterKey, folder);
				string location = Helper.getFolderLocation(masterKey, folder); //metaDataKey
				string fdPrivate = AesGcm256.toHex(folderKey.PrivateKey.ToBytes()).ToLower();
				string keyString = Sha3Keccack.Current.CalculateHash(fdPrivate);

				//Get latest meta and add new file
				FolderMetaData metaData = GetFolderMetaData(folder);

				//Clean out bug deleted files
				metaData.Files.RemoveAll(o => o.Versions.Any() == false);

				//Find the file
				FolderMetaFile oldEntry = metaData.Files.FirstOrDefault(o => o.Name == oldName);
				oldEntry.Name = newName;

				string metaDataMinifiedJSON = metaData.Minify(cacheFolderStructure);

				//Serialize and encrypt
				//string metaDataJSON = JsonConvert.SerializeObject(metaData);
				byte[] encryptedMetaData = AesGcm256.encryptString(metaDataMinifiedJSON, AesGcm256.HexToByte(keyString));
				//string encryptedMetaDataHex = AesGcm256.toHex(encryptedMetaData).ToLower();
				string encryptedMetaDataBase64 = System.Convert.ToBase64String(encryptedMetaData);

				//SET FOLDER METADATA
				var timestamp = Helper.GetUnixMilliseconds();
				Dictionary<string, object> metaReqDict = new Dictionary<string, object>
			{
				{"timestamp", timestamp},
				{"metadataKey", location },
				{"metadata", encryptedMetaDataBase64 }
			};
				var payloadMetaJSON = JsonConvert.SerializeObject(metaReqDict);
				var payloadMeta = Crypto.SignPayloadDict(payloadMetaJSON, "requestBody", new Dictionary<string, ByteArrayContent>
				{
				}, privateKey);

				string json = JsonConvert.SerializeObject(payloadMeta);

				RestClient client = new RestClient("https://broker-1.opacitynodes.com:3000/api/v1/metadata/set");
				var request = new RestRequest("", Method.POST, DataFormat.Json);
				request.AddJsonBody(payloadMeta);
				var response = client.Post(request);
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
				throw;
			}
		}

		public async Task<string> Upload(string filePath, string folder)
		{
			string handleHex = "";
			await Task.Run(() =>
			{
				FileInfo info = new FileInfo(filePath);
				FileData fd = Helper.GetFileData(info);
				FileMetaData metaData = FileMetaData.CreateMetaData(fd, new FileMetaOptions
				{
					BlockSize = Constants.DEFAULT_BLOCK_SIZE,
					PartSize = 10485760//Constants.DEFAULT_PART_SIZE // 10485760//Constants.DEFAULT_PART_SIZE//10485760 //10485760
				});
				long uploadSize = Helper.GetUploadSize(info.Length);
				int endindex = Helper.GetEndIndex(uploadSize, metaData.P);

				//const handle = hash32 + key32;
				byte[] handle = Helper.GenerateFileKeys();
				byte[] hash = new byte[32];
				byte[] key = new byte[32];
				Array.Copy(handle, 0, hash, 0, 32);
				Array.Copy(handle, 32, key, 0, 32);

				string metaDataJSON = JsonConvert.SerializeObject(metaData);

				byte[] encryptedMetaData = AesGcm256.encryptString(metaDataJSON, key);

				handleHex = AesGcm256.toHex(handle).ToLower();
				//Use Hash here as fileid?
				string fileId = AesGcm256.toHex(hash).ToLowerInvariant();

				Dictionary<string, object> requestBody = new Dictionary<string, object>();
				requestBody.Add("fileHandle", fileId);
				requestBody.Add("fileSizeInByte", uploadSize);
				requestBody.Add("endIndex", endindex);

				string requestBodyJSON = JsonConvert.SerializeObject(requestBody);
				var payload = Crypto.SignPayloadForm(requestBodyJSON, "requestBody", new Dictionary<string, ByteArrayContent>
			{
				{"metadata", new ByteArrayContent(encryptedMetaData) }
			}, privateKey);
				//AesGcm256.enc


				Helper.PostFormData(payload, "https://broker-1.opacitynodes.com:3000/api/v1/init-upload").Wait();

				int left = endindex;
				//Parallel of equivalent for (int id = 0; id < endindex; id++)
				Parallel.For(0, endindex, new ParallelOptions { MaxDegreeOfParallelism = 8 }, (id, loopState) =>
				 {
					 left--;
					 Console.WriteLine($"Uploading file {info.Name} part {id}/{endindex} parts left: {left}");
					 uploadPart(info, metaData, handle, id, endindex);
				 });

				//* CONFIRM UPLOAD *//
				requestBody = new Dictionary<string, object>();
				requestBody.Add("fileHandle", fileId);
				requestBodyJSON = JsonConvert.SerializeObject(requestBody);
				var payloadUpdateStatus = Crypto.SignPayloadDict(requestBodyJSON, "requestBody", new Dictionary<string, ByteArrayContent>
				{
				}, privateKey);

				string payloadUpdateStatusJSON = JsonConvert.SerializeObject(payloadUpdateStatus);

				RestClient client = new RestClient("https://broker-1.opacitynodes.com:3000/api/v1/upload-status");
				RestRequest request = new RestRequest("", Method.POST, DataFormat.Json);
				request.AddJsonBody(payloadUpdateStatusJSON);
				var response = client.Post(request);

				string metaDataUpdate = response.Content;
				FileUploadStatus status = JsonConvert.DeserializeObject<FileUploadStatus>(metaDataUpdate);

				int retryCount = 1;
				//Retry missing parts
				while (status.MissingIndexes.Count > 0 && retryCount < 4)
				{
					Console.WriteLine($"Resending missing chunks [try:{retryCount}]: {string.Join(",", status.MissingIndexes.ConvertAll<string>(o => o.ToString()))}");
					foreach (int missingId in status.MissingIndexes)
					{
						uploadPart(info, metaData, handle, missingId - 1, endindex + retryCount);
					}
					response = client.Post(request);
					status = JsonConvert.DeserializeObject<FileUploadStatus>(response.Content);
					retryCount++;
				}

				//Add the file to the folder metadata
				FolderMetaFile file = new FolderMetaFile();
				file.Created = Helper.GetUnixMilliseconds(); ;
				file.Name = fd.Name;
				file.Type = "file";
				file.Versions.Add(new FolderMetaFileVersion
				{
					Handle = handleHex,
					Modified = file.Created,
					Created = file.Created,
					Size = info.Length
				});

				AddFileToFolderMetaData(folder, file);

			});
			return handleHex;
		}

		private bool uploadPart(FileInfo fileInfo, FileMetaData metaData, byte[] handle, int index, int endIndex)
		{
			//Init
			long raw = 0;
			long encrypt = 0;

			//Get handle components
			byte[] hash = new byte[32];
			byte[] key = new byte[32];
			Array.Copy(handle, 0, hash, 0, 32);
			Array.Copy(handle, 32, key, 0, 32);
			string handleHex = AesGcm256.toHex(handle).ToLower();
			string fileId = AesGcm256.toHex(hash).ToLowerInvariant();

			long partSize = metaData.P.PartSize;// - Constants.BLOCK_OVERHEAD;

			//Take raw part from file
			byte[] rawpart = Helper.GetPartial(fileInfo, partSize, index);
			int blockSize = metaData.P.BlockSize;

			raw += rawpart.Length;

			//Encrypt chunks
			//int numChunks = (int)(rawpart.Length / blockSize) + 1;
			int numChunks = (int)Math.Ceiling((double)rawpart.Length / (double)blockSize);

			byte[] encryptedBlob = new byte[0];
			using (MemoryStream ms = new MemoryStream())
			{
				for (int i = 0; i < numChunks; i++)
				{
					//Take chunk
					long remaining = (rawpart.Length - (long)i * (long)blockSize);
					if (remaining <= 0)
						break;

					int chunkSize = (int)Math.Min(remaining, (long)blockSize);
					int encryptedChunkSize = chunkSize + Constants.BLOCK_OVERHEAD;

					byte[] chunk = new byte[chunkSize];
					Array.Copy(rawpart, i * blockSize, chunk, 0, chunkSize);
					byte[] encryptedChunk = AesGcm256.encrypt(chunk, key);
					ms.Write(encryptedChunk, 0, encryptedChunkSize);
					if (encryptedChunkSize != encryptedChunk.Length)
					{
						Debugger.Break();
					}
					if (chunkSize < blockSize)
					{
						//Debugger.Break();
					}
				}
				ms.Flush();
				encryptedBlob = ms.ToArrayEfficient();
			}

			encrypt += encryptedBlob.Length;

			//Encrypt chunk and post
			Dictionary<string, object> requestBody = new Dictionary<string, object>();
			requestBody.Add("fileHandle", fileId);
			requestBody.Add("partIndex", index + 1);
			requestBody.Add("endIndex", endIndex);

			string requestBodyJSON = JsonConvert.SerializeObject(requestBody);

			var payload = Crypto.SignPayloadForm(requestBodyJSON, "requestBody", new Dictionary<string, ByteArrayContent>
				{
					{"chunkData", new ByteArrayContent(encryptedBlob) }
				}, privateKey);
			//AesGcm256.enc
			try
			{
				Helper.PostFormData(payload, "https://broker-1.opacitynodes.com:3000/api/v1/upload").Wait();
				return true;
			}
			catch (Exception e)
			{
				return false;
			}
		}

		public static async Task Download(string fileHandle, string saveToPath)
		{
			await Task.Run(() =>
			{
				string fileid = fileHandle.Substring(0, 64).ToLower();
				string filekey = fileHandle.Substring(64, 64).ToLower();
				byte[] key = AesGcm256.HexToByte(filekey);

				//Get file metadata
				var client = new RestClient("https://s3.us-east-2.amazonaws.com/opacity-prod/" + fileid + "/metadata");
				var request = new RestRequest("", DataFormat.Json);
				var response = client.Get(request);
				byte[] encryptedMetaData = response.RawBytes;

				//Decrypt filemetadata
				string metaDataStr = Helper.BlobToString(AesGcm256.decrypt
			  (encryptedMetaData, key));
				FileMetaData metaData = JsonConvert.DeserializeObject<FileMetaData>(metaDataStr);

				//range: bytes=0-5245439
				long uploadSize = Helper.GetUploadSize(metaData.Size);
				long partSize = 5245440;//Constants.DEFAULT_PART_SIZE;//metaData.P.PartSize;//80 * (Constants.DEFAULT_BLOCK_SIZE + Constants.BLOCK_OVERHEAD);//10485760 + Constants.;////Constants.DEFAULT_PART_SIZE;
				int parts = (int)(uploadSize / partSize) + 1;
				//int parts = (int)Math.Ceiling((double)uploadSize / (double)partSize);
				byte[] responseData = new byte[0];

				string[] fileSplit = metaData.Name.Split('.');
				fileSplit[fileSplit.Length - 1] = "";
				string fileName = string.Join("", fileSplit);

				string folderPath = saveToPath + $"/tmp/{fileName}/";
				Directory.CreateDirectory(folderPath);

				string url = "https://s3.us-east-2.amazonaws.com/opacity-prod/" + fileid + "/file";
				client = new RestClient("https://s3.us-east-2.amazonaws.com/opacity-prod/" + fileid + "/file");

				ParallelLoopResult parallelResult = Parallel.For(0, parts, new ParallelOptions { MaxDegreeOfParallelism = 5 }, (id, loopState) =>
				{
					long byteFrom = id * partSize;
					long byteTo = (id + 1) * partSize - 1;
					if (byteTo > uploadSize - 1)
						byteTo = uploadSize - 1;
					//Console.WriteLine("{0} : {1}", byteFrom, byteTo);

					//var byteRequest = new RestRequest("", DataFormat.Json);
					//byteRequest.AddHeader("range", $"bytes={byteFrom}-{byteTo - 1}");
					//var byteResponse = client.Get(byteRequest);

					using (HttpClient httpClient = new HttpClient())
					{
						httpClient.DefaultRequestHeaders.Add("range", $"bytes={byteFrom}-{byteTo}");
						using (HttpResponseMessage httpResponse = httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result)
						using (Stream streamToReadFrom = httpResponse.Content.ReadAsStreamAsync().Result)
						{
							string fileToWriteTo = folderPath + id + ".part";
							using (Stream streamToWriteTo = File.Open(fileToWriteTo, FileMode.Create))
							{
								streamToReadFrom.CopyTo(streamToWriteTo);
							}
						}
					}
				});
				while (!parallelResult.IsCompleted)
				{
					Thread.Sleep(100);
				}

				int chunkSize = metaData.P.BlockSize + Constants.BLOCK_OVERHEAD;
				int numChunks = (int)(uploadSize / chunkSize) + 1;
				//int numChunks = (int)Math.Ceiling((double)uploadSize / (double)chunkSize);

				string path = saveToPath + "\\" + metaData.Name;
				if (File.Exists(path))
				{
					File.Delete(path);
				}

				//Decrypt filestream
				using (var stream = new FileStream(path, FileMode.Append))
				{
					int id = 0;
					long seek = 0;
					for (int i = 0; i < numChunks; i++)
					{

						byte[] chunkRaw;
						using (var chunkStream = new FileStream(folderPath + id + ".part", FileMode.Open))
						{
							chunkStream.Seek(seek, SeekOrigin.Begin);
							int take = chunkSize;
							if (seek + take >= chunkStream.Length)
							{
								long remaining = chunkStream.Length - seek;
								take = (int)remaining;
							}

							chunkRaw = chunkStream.ReadBytes(take);
							seek += chunkSize;
							if (seek >= chunkStream.Length - 1)
							{
								seek = 0;
								id++;
							}
						}

						//Decrypt
						byte[] decryptedChunk = AesGcm256.decrypt(chunkRaw, key);
						stream.Write(decryptedChunk, 0, decryptedChunk.Length);
					}
				}
				Console.WriteLine("Download finished: " + metaData.Name);

				Directory.Delete(folderPath, true);
			});
		}
	}
}
