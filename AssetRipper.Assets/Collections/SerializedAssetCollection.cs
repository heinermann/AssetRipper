﻿using AssetRipper.Assets.Bundles;
using AssetRipper.Assets.IO;
using AssetRipper.Assets.IO.Reading;
using AssetRipper.Assets.Metadata;
using AssetRipper.IO.Endian;
using AssetRipper.IO.Files.SerializedFiles;
using AssetRipper.IO.Files.SerializedFiles.Parser;
using AssetRipper.IO.Files.Streams;
using AssetRipper.VersionUtilities;

namespace AssetRipper.Assets.Collections;

/// <summary>
/// A collection of assets read from a <see cref="SerializedFile"/>.
/// </summary>
public class SerializedAssetCollection : AssetCollection
{
	private FileIdentifier[] DependencyIdentifiers { get; set; } = Array.Empty<FileIdentifier>();

	protected SerializedAssetCollection(Bundle bundle) : base(bundle)
	{
	}

	protected override bool IsCompatibleDependency(AssetCollection dependency)
	{
		return dependency is SerializedAssetCollection or ProcessedAssetCollection;
	}

	public void InitializeDependencyList()
	{
		if (Dependencies.Count > 1)
		{
			throw new Exception("Dependency list has already been initialized.");
		}
		for (int i = 0; i < DependencyIdentifiers.Length; i++)
		{
			FileIdentifier identifier = DependencyIdentifiers[i];
			SetDependency(i + 1, Bundle.ResolveCollection(identifier));
		}
		DependencyIdentifiers = Array.Empty<FileIdentifier>();
	}

	internal static SerializedAssetCollection FromSerializedFile(Bundle bundle, SerializedFile file, AssetFactory factory)
	{
		SerializedAssetCollection collection = new SerializedAssetCollection(bundle)
		{
			Name = file.NameFixed,
			Version = file.Version,
			Platform = file.Platform,
			Flags = file.Flags,
			EndianType = file.EndianType,
		};
		FileIdentifier[] fileDependencies = file.Metadata.Externals;
		if (fileDependencies.Length > 0)
		{
			collection.DependencyIdentifiers = new FileIdentifier[fileDependencies.Length];
			Array.Copy(fileDependencies, collection.DependencyIdentifiers, fileDependencies.Length);
		}
		ReadData(collection, file, factory);
		return collection;
	}

	private static void ReadData(SerializedAssetCollection collection, SerializedFile file, AssetFactory factory)
	{
		if (SerializedFileMetadata.HasScriptTypes(file.Header.Version))
		{
			for (int i = 0; i < file.Metadata.ScriptTypes.Length; i++)
			{
				LocalSerializedObjectIdentifier ptr = file.Metadata.ScriptTypes[i];
				if (ptr.LocalSerializedFileIndex == 0)
				{
					int index = file.AssetEntryLookup[ptr.LocalIdentifierInFile];
					ObjectInfo objectInfo = file.Metadata.Object[index];
					ReadAsset(collection, file, objectInfo, factory);
				}
			}
		}

		for (int i = 0; i < file.Metadata.Object.Length; i++)
		{
			ObjectInfo objectInfo = file.Metadata.Object[i];
			if (objectInfo.ClassID == 115)//MonoScript
			{
				if (!collection.Assets.ContainsKey(objectInfo.FileID))
				{
					ReadAsset(collection, file, objectInfo, factory);
				}
			}
		}

		for (int i = 0; i < file.Metadata.Object.Length; i++)
		{
			ObjectInfo objectInfo = file.Metadata.Object[i];
			if (!collection.Assets.ContainsKey(objectInfo.FileID))
			{
				ReadAsset(collection, file, objectInfo, factory);
			}
		}
	}

	private static void ReadAsset(SerializedAssetCollection collection, SerializedFile file, ObjectInfo info, AssetFactory factory)
	{
		AssetInfo assetInfo = new AssetInfo(collection, info.FileID, info.ClassID);
		long offset = file.Header.DataOffset + info.ByteStart;
		int size = info.ByteSize;
		SerializedType type = info.GetSerializedType(file.Metadata.Types);
		using PartialStream partialStream = new PartialStream(file.Stream, offset, size, true);
		using AssetReader reader = new AssetReader(partialStream, collection);
		IUnityObjectBase? asset = factory.ReadAsset(assetInfo, reader, size, type);
		if (asset is not null)
		{
			collection.AddAsset(asset);
		}
	}
}
