using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OpaqueSharp
{
	public class AccountStatus
	{
		public DateTime createdAt { get; set; }
		public DateTime updatedAt { get; set; }
		public DateTime expirationDate { get; set; }
		public int monthsInSubscription { get; set; }
		public int storageLimit { get; set; }
		public double storageUsed { get; set; }
		public string ethAddress { get; set; }
		public int cost { get; set; }
		public int apiVersion { get; set; }
		public int totalFolders { get; set; }
		public double totalMetadataSizeInMB { get; set; }
		public int maxFolders { get; set; }
		public int maxMetadataSizeInMB { get; set; }
	}

	public class StripeData
	{
		public bool stripePaymentExists { get; set; }
		public bool chargePaid { get; set; }
		public string stripeToken { get; set; }
		public string opqTxStatus { get; set; }
		public string chargeID { get; set; }
		public int amount { get; set; }
	}

	public class PaymentStatus
	{
		public string paymentStatus { get; set; }
		public object error { get; set; }
		public AccountStatus account { get; set; }
		public StripeData stripeData { get; set; }
	}
}
