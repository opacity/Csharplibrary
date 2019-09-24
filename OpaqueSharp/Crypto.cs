using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Signer.Crypto;
using Nethereum.Util;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace OpaqueSharp
{
	public class Crypto
	{
		public Crypto()
		{
		}

		public static MultipartFormDataContent SignPayloadForm(string rawPayload, string rawKey, Dictionary<string, ByteArrayContent> extraPayload, string privateKey)
		{
			var privKey0 = new EthECKey(privateKey);
			byte[] pubKeyCompressed = new ECKey(privKey0.GetPrivateKeyAsBytes(), true).GetPubKey(true);
			string pubHex = pubKeyCompressed.ToHex();

			byte[] msgBytes = Encoding.UTF8.GetBytes(rawPayload);
			byte[] msgHash = new Sha3Keccack().CalculateHash(msgBytes);
			string msgHashHex = msgHash.ToHex();

			var signatureTEST = privKey0.SignAndCalculateV(msgHash);
			string sigFinal0 = signatureTEST.V[0] - 27 + signatureTEST.R.ToHex() + signatureTEST.S.ToHex();
			string sigFinal1 = signatureTEST.R.ToHex() + signatureTEST.S.ToHex();

			string stigFinal = signatureTEST.R.ToHex().PadLeft(64, '0') + signatureTEST.S.ToHex().PadLeft(64, '0');


			HttpClient httpClient = new HttpClient();
			MultipartFormDataContent form = new MultipartFormDataContent();
			form.Add(new StringContent(rawPayload), rawKey);
			form.Add(new StringContent(stigFinal), "signature");
			form.Add(new StringContent(pubHex), "publicKey");

			foreach (var extraContent in extraPayload)
			{
				form.Add(extraContent.Value, extraContent.Key, extraContent.Key);
			}
			if (extraPayload.Count > 0)
				form.Last().Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
			else
				form.Add(new StringContent(msgHashHex), "hash");

			return form;
		}

		public static Dictionary<string, string> SignPayloadDict(string rawPayload, string rawKey, Dictionary<string, ByteArrayContent> extraPayload, string privateKey)
		{
			var privKey0 = new EthECKey(privateKey);
			byte[] pubKeyCompressed = new ECKey(privKey0.GetPrivateKeyAsBytes(), true).GetPubKey(true);
			string pubHex = pubKeyCompressed.ToHex();

			byte[] msgBytes = Encoding.UTF8.GetBytes(rawPayload);
			byte[] msgHash = new Sha3Keccack().CalculateHash(msgBytes);
			string msgHashHex = msgHash.ToHex();


			var signatureTEST = privKey0.SignAndCalculateV(msgHash);

			string sigFinal0 = signatureTEST.V[0] - 27 + signatureTEST.R.ToHex() + signatureTEST.S.ToHex();
			string sigFinal1 = signatureTEST.R.ToHex() + signatureTEST.S.ToHex();

			string stigFinal = signatureTEST.R.ToHex().PadLeft(64, '0') + signatureTEST.S.ToHex().PadLeft(64, '0');


			/*if (signatureTEST.V[0] != 27)
			{

			}*/

			if (sigFinal1.Length != 128)
			{
				sigFinal1 = "";
				if (signatureTEST.R.Length != 32)
				{
					sigFinal1 += "00" + signatureTEST.R.ToHex();
				}
				else
				{
					sigFinal1 += signatureTEST.R.ToHex();
				}
				if (signatureTEST.S.Length != 32)
				{
					sigFinal1 += "00" + signatureTEST.S.ToHex();
				}
				else
				{
					sigFinal1 += signatureTEST.S.ToHex();
				}
			}
			
			HttpClient httpClient = new HttpClient();
			Dictionary<string, string> dict = new Dictionary<string, string>();
			dict.Add(rawKey, rawPayload);
			dict.Add("signature", stigFinal);
			dict.Add("publicKey", pubHex);
			dict.Add("hash", msgHashHex);

			return dict;
		}

		public static MultipartFormDataContent getPayload(string rawPayload, string rawKey, Dictionary<string, ByteArrayContent> extraPayload, string privateKey)
		{
			Nethereum.Web3.Accounts.Account acc = new Nethereum.Web3.Accounts.Account(privateKey);
			EthECKey privKey = new EthECKey(privateKey);
			var signer = new EthereumMessageSigner();
			Sha3Keccack kec = new Sha3Keccack();
			byte[] hash0 = kec.CalculateHash(Encoding.UTF8.GetBytes(rawPayload));
			byte[] hash = signer.Hash(Encoding.UTF8.GetBytes(rawPayload));
			string signature = signer.EncodeUTF8AndSign(rawPayload, privKey);
			var addressRec1 = signer.EncodeUTF8AndEcRecover(rawPayload, signature);

			string sig2 = signer.HashAndSign(rawPayload, privateKey);
			signature = signature.Remove(0, 2);

			string privEx = privKey.GetPrivateKey();

			byte[] pubKey = privKey.GetPubKey();
			byte[] pubKeyNoPrefix = privKey.GetPubKeyNoPrefix();
			string pubAddr = privKey.GetPublicAddress();

			string pubHex = AesGcm256.toHex(pubKey).ToLowerInvariant();
			string pubNoPrefixHex = AesGcm256.toHex(pubKeyNoPrefix).ToLowerInvariant();

			HttpClient httpClient = new HttpClient();
			MultipartFormDataContent form = new MultipartFormDataContent();

			//Fix pubkey format
			pubHex = pubHex.Substring(0, 66);
			pubHex = "033" + pubHex;
			form.Add(new StringContent(rawPayload), rawKey);
			form.Add(new StringContent(signature), "signature");
			form.Add(new StringContent(pubHex), "publicKey");

			EthereumMessageSigner sign2 = new EthereumMessageSigner();
			byte[] payloadBytes = Encoding.UTF8.GetBytes(rawPayload);
			string recover = sign2.EcRecover(payloadBytes, signature);

			foreach (var extraContent in extraPayload)
			{
				form.Add(extraContent.Value, extraContent.Key, extraContent.Key);
			}
			form.Last().Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

			return form;
		}
	}
}
