using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Text;

namespace OpaqueSharp
{
	public class AesGcm256
	{
		private static readonly SecureRandom Random = new SecureRandom();
		private AesGcm256() { }

		public static byte[] NewIv()
		{
			var iv = new byte[Constants.IV_BYTE_LENGTH];
			Random.NextBytes(iv);
			return iv;
		}

		public static Byte[] HexToByte(string hexStr)
		{
			byte[] bArray = new byte[hexStr.Length / 2];
			for (int i = 0; i < (hexStr.Length / 2); i++)
			{
				byte firstNibble = Byte.Parse(hexStr.Substring((2 * i), 1),
								   System.Globalization.NumberStyles.HexNumber); // [x,y)
				byte secondNibble = Byte.Parse(hexStr.Substring((2 * i) + 1, 1),
									System.Globalization.NumberStyles.HexNumber);
				int finalByte = (secondNibble) | (firstNibble << 4); // bit-operations 
																	 // only with numbers, not bytes.
				bArray[i] = (byte)finalByte;
			}
			return bArray;
		}

		public static string toHex(byte[] data)
		{
			string hex = string.Empty;
			foreach (byte c in data)
			{
				hex += c.ToString("X2");
			}
			return hex;
		}

		public static string toHex(string asciiString)
		{
			string hex = string.Empty;
			foreach (char c in asciiString)
			{
				int tmp = c;
				hex += string.Format("{0:x2}", System.Convert.ToUInt32(tmp.ToString()));
			}
			return hex;
		}

		public static byte[] encryptString(string plainText, byte[] key)
		{
			return encrypt(Encoding.UTF8.GetBytes(plainText), key);
		}

		public static byte[] encrypt(byte[] data, byte[] key)
		{
			byte[] iv = NewIv();
			byte[] tag = new byte[0];
			byte[] encryptedBytes = new byte[0];
			try
			{

				GcmBlockCipher cipher = new GcmBlockCipher(new AesFastEngine());
				AeadParameters parameters =
						 new AeadParameters(new KeyParameter(key), Constants.TAG_BIT_LENGTH, iv);

				cipher.Init(true, parameters);

				encryptedBytes = new byte[cipher.GetOutputSize(data.Length)];
				Int32 retLen = cipher.ProcessBytes
							   (data, 0, data.Length, encryptedBytes, 0);
				cipher.DoFinal(encryptedBytes, retLen);
				tag = cipher.GetMac();
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				Console.WriteLine(ex.StackTrace);
			}

			int finalLength = encryptedBytes.Length + Constants.IV_BYTE_LENGTH;
			byte[] finalBytes = new byte[finalLength];
			Array.Copy(encryptedBytes, 0, finalBytes, 0, encryptedBytes.Length);
			Array.Copy(iv, 0, finalBytes, encryptedBytes.Length, Constants.IV_BYTE_LENGTH);

			return finalBytes;
		}

		public static byte[] decryptString(string encryptedText, byte[] key)
		{
			return decrypt(Convert.FromBase64String(encryptedText), key);
		}

		public static byte[] decrypt(byte[] encryptedBytes, byte[] key)
		{
			const int overhead = 32; //16 byte tag 16 byte iv
			byte[] raw = new byte[encryptedBytes.Length - overhead];
			byte[] tag = new byte[16];
			byte[] iv = new byte[16];

			Array.Copy(encryptedBytes, 0, raw, 0, raw.Length);
			Array.Copy(encryptedBytes, raw.Length, tag, 0, tag.Length);
			Array.Copy(encryptedBytes, raw.Length + tag.Length, iv, 0, iv.Length);

			byte[] final = new byte[0];
			try
			{
				GcmBlockCipher cipher = new GcmBlockCipher(new AesFastEngine());
				AeadParameters parameters =
					  new AeadParameters(new KeyParameter(key), Constants.TAG_BIT_LENGTH, iv);

				cipher.Init(true, parameters);
				byte[] plainBytes = new byte[cipher.GetOutputSize(raw.Length)];
				Int32 retLen = cipher.ProcessBytes
							   (raw, 0, raw.Length, plainBytes, 0);
				cipher.DoFinal(plainBytes, retLen);

				final = new byte[plainBytes.Length - 16];
				Array.Copy(plainBytes, final, plainBytes.Length - 16);

				return final;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				Console.WriteLine(ex.StackTrace);
			}

			return final;
		}
	}
}