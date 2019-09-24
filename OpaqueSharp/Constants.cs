namespace OpaqueSharp
{
	public struct Constants
	{
		public const short FILENAME_MAX_LENGTH = 256;
		public const short CURRENT_VERSION = 1;
		public const short IV_BYTE_LENGTH = 16;
		public const short TAG_BYTE_LENGTH = 16;
		public const short TAG_BIT_LENGTH = TAG_BYTE_LENGTH * 8;
		public const int DEFAULT_BLOCK_SIZE = 64 * 1024;
		public const short BLOCK_OVERHEAD = TAG_BYTE_LENGTH + IV_BYTE_LENGTH;
		public const long DEFAULT_PART_SIZE = 128 * (DEFAULT_BLOCK_SIZE + BLOCK_OVERHEAD); //128 * 
	}
}
