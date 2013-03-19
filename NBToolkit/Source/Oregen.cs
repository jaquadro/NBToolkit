﻿using System;
using System.Collections.Generic;
using System.Text;
using NDesk.Options;
using Substrate;
using Substrate.Core;

namespace NBToolkit
{
    public class OregenOptions : TKOptions, IChunkFilterable
    {
        private OptionSet _filterOpt = null;
        private ChunkFilter _chunkFilter = null;
        private BlockFilter _blockFilter = null;  

        public int? OPT_ID = null;

        public int? OPT_DATA = null;

        public int? OPT_ROUNDS = null;
        public int? OPT_SIZE = null;
        public int? OPT_MIN = null;
        public int? OPT_MAX = null;

        public bool OPT_OO = false;
        public bool OPT_OA = false;

        public bool OPT_MATHFIX = true;
        
        public List<int> OPT_OB_INCLUDE = new List<int>();
        public List<int> OPT_OB_EXCLUDE = new List<int>();

        private class OreType
        {
            public int id;
            public string name;
            public int rounds;
            public int min;
            public int max;
            public int size;
        };

        private OreType[] oreList = new OreType[] {
            new OreType() { id = 16, name = "Coal", rounds = 20, min = 0, max = 127, size = 16 },
            new OreType() { id = 15, name = "Iron", rounds = 20, min = 0, max = 63, size = 8 },
            new OreType() { id = 14, name = "Gold", rounds = 2, min = 0, max = 31, size = 8 },
            new OreType() { id = 73, name = "Redstone", rounds = 8, min = 0, max = 15, size = 7 },
            new OreType() { id = 56, name = "Diamond", rounds = 1, min = 0, max = 15, size = 7 },
            new OreType() { id = 21, name = "Lapis", rounds = 1, min = 0, max = 31, size = 7 },
            new OreType() { id = 153, name = "Quartz", rounds = 16, min = 10, max = 117, size = 13 },
        };

        public OregenOptions ()
            : base()
        {            
            _filterOpt = new OptionSet()
            {
                { "b|Block=", "Generate blocks of type {ID} (0-255)",
                    v => OPT_ID = Convert.ToByte(v) % 256 },
                { "d|Data=", "Set the block's data value to {VAL} (0-15)",
                    v => OPT_DATA = Convert.ToInt32(v) % 16 },
                { "r|Rounds=", "Geneate {NUM} deposits per chunk",
                    v => OPT_ROUNDS = Convert.ToInt32(v) },
                { "min|MinDepth=", "Generates deposits no lower than depth {VAL} (0-127)",
                    v => OPT_MIN = Convert.ToInt32(v) % 128 },
                { "max|MaxDepth=", "Generates deposits no higher than depth {VAL} (0-127)",
                    v => OPT_MAX = Convert.ToInt32(v) % 128 },
                { "s|Size=", "Generates deposits containing roughly up to {VAL} blocks",
                    v => OPT_SIZE = Convert.ToInt32(v) % 128 },
                { "oo|OverrideOres", "Generated deposits can replace other existing ores",
                    v => OPT_OO = true },
                { "oa|OverrideAll", "Generated deposits can replace any existing block",
                    v => OPT_OA = true },
                { "oi|OverrideInclude=", "Generated deposits can replace the specified block type {ID} [repeatable]",
                    v => OPT_OB_INCLUDE.Add(Convert.ToInt32(v) % 256) },
                { "ox|OverrideExclude=", "Generated deposits can never replace the specified block type {ID} [repeatable]",
                    v => OPT_OB_EXCLUDE.Add(Convert.ToInt32(v) % 256) },
                { "nu|NativeUnpatched", "Use MC native ore generation algorithm without distribution evenness patch",
                    v => OPT_MATHFIX = false },
            };

            _chunkFilter = new ChunkFilter();
            _blockFilter = new BlockFilter();
        }

        public OregenOptions (string[] args)
            : this()
        {
            Parse(args);
        }

        public override void Parse (string[] args)
        {
            base.Parse(args);

            _filterOpt.Parse(args);
            _chunkFilter.Parse(args);
            _blockFilter.Parse(args);
        }

        public override void PrintUsage ()
        {
            Console.WriteLine("Usage: nbtoolkit oregen -b <id> -w <path> [options]");
            Console.WriteLine();
            Console.WriteLine("Options for command 'oregen':");

            _filterOpt.WriteOptionDescriptions(Console.Out);

            Console.WriteLine();
            _chunkFilter.PrintUsage();

            Console.WriteLine();
            _blockFilter.PrintUsage();

            Console.WriteLine();
            base.PrintUsage();
        }

        public override void SetDefaults ()
        {
            base.SetDefaults();

            foreach (OreType ore in oreList) {
                if (OPT_ID != ore.id) {
                    continue;
                }

                if (OPT_ROUNDS == null) {
                    OPT_ROUNDS = ore.rounds;
                }
                if (OPT_MIN == null) {
                    OPT_MIN = ore.min;
                }
                if (OPT_MAX == null) {
                    OPT_MAX = ore.max;
                }
                if (OPT_SIZE == null) {
                    OPT_SIZE = ore.size;
                }
            }

            // Check for required parameters
            if (OPT_ID == null) {
                Console.WriteLine("Error: You must specify a Block ID");
                Console.WriteLine();
                PrintUsage();

                throw new TKOptionException();
            }

            if (OPT_ROUNDS == null) {
                OPT_ROUNDS = 1;
            }

            if (OPT_MIN == null || OPT_MAX == null || OPT_SIZE == null) {
                if (OPT_MIN == null) {
                    Console.WriteLine("Error: You must specify the minimum depth for non-ore blocks");
                }
                if (OPT_MAX == null) {
                    Console.WriteLine("Error: You must specify the maximum depth for non-ore blocks");
                }
                if (OPT_SIZE == null) {
                    Console.WriteLine("Error: You must specify the deposit size for non-ore blocks");
                }

                Console.WriteLine();
                PrintUsage();

                throw new TKOptionException();
            }
        }

        public IChunkFilter GetChunkFilter ()
        {
            return _chunkFilter;
        }

        public IBlockFilter GetBlockFilter ()
        {
            return _blockFilter;
        }
    }

    public class Oregen : TKFilter
    {
        private OregenOptions opt;

        private static Random rand = new Random();

        public Oregen (OregenOptions o)
        {
            opt = o;
        }

        public override void Run ()
        {
            NbtWorld world = NbtWorld.Open(opt.OPT_WORLD);
            IChunkManager cm = world.GetChunkManager(opt.OPT_DIM);
            FilteredChunkManager fcm = new FilteredChunkManager(cm, opt.GetChunkFilter());

            int affectedChunks = 0;
            foreach (ChunkRef chunk in fcm) {
                if (chunk == null || !chunk.IsTerrainPopulated) {
                    continue;
                }

                if (opt.OPT_V) {
                    Console.WriteLine("Processing Chunk (" + chunk.X + "," + chunk.Z + ")");
                }

                affectedChunks++;

                ApplyChunk(world, chunk);

                fcm.Save();
            }

            Console.WriteLine("Affected Chunks: " + affectedChunks);
        }

        public void ApplyChunk (NbtWorld world, ChunkRef chunk)
        {
            if (opt.OPT_V) {
                Console.WriteLine("Generating {0} size {1} deposits of {2} between {3} and {4}",
                    opt.OPT_ROUNDS, opt.OPT_SIZE, opt.OPT_ID, opt.OPT_MIN, opt.OPT_MAX);
            }

            IGenerator generator;
            if (opt.OPT_DATA == null) {
                generator = new NativeGenOre((int)opt.OPT_ID, (int)opt.OPT_SIZE);
                ((NativeGenOre)generator).MathFix = opt.OPT_MATHFIX;
            }
            else {
                generator = new NativeGenOre((int)opt.OPT_ID, (int)opt.OPT_DATA, (int)opt.OPT_SIZE);
                ((NativeGenOre)generator).MathFix = opt.OPT_MATHFIX;
            }

            IChunkManager cm = world.GetChunkManager(opt.OPT_DIM);
            IBlockManager bm = new GenOreBlockManager(cm, opt);

            for (int i = 0; i < opt.OPT_ROUNDS; i++) {
                if (opt.OPT_VV) {
                    Console.WriteLine("Generating round {0}...", i);
                }

                int x = chunk.X * chunk.Blocks.XDim + rand.Next(chunk.Blocks.XDim);
                int y = (int)opt.OPT_MIN + rand.Next((int)opt.OPT_MAX - (int)opt.OPT_MIN);
                int z = chunk.Z * chunk.Blocks.ZDim + rand.Next(chunk.Blocks.ZDim);

                generator.Generate(bm, rand, x, y, z);
            }
        }
    }

    public class GenOreBlockManager : BlockManager
    {
        public const int BLOCK_STONE = 1;
        public const int BLOCK_DIRT = 3;
        public const int BLOCK_GRAVEL = 13;
        public const int BLOCK_GOLD = 14;
        public const int BLOCK_IRON = 15;
        public const int BLOCK_COAL = 16;
        public const int BLOCK_LAPIS = 21;
        public const int BLOCK_DIAMOND = 56;
        public const int BLOCK_REDSTONE = 73;

        protected OregenOptions opt;

        private static Random rand = new Random();

        public GenOreBlockManager (IChunkManager bm, OregenOptions o)
            : base(bm)
        {
            opt = o;

            IChunk c = null;

            if (bm is AlphaChunkManager)
                c = AlphaChunk.Create(0, 0);
            else
                c = AnvilChunk.Create(0, 0);

            chunkXDim = c.Blocks.XDim;
            chunkYDim = c.Blocks.YDim;
            chunkZDim = c.Blocks.ZDim;
            chunkXMask = chunkXDim - 1;
            chunkYMask = chunkYDim - 1;
            chunkZMask = chunkZDim - 1;
            chunkXLog = Log2(chunkXDim);
            chunkYLog = Log2(chunkYDim);
            chunkZLog = Log2(chunkZDim);
        }

        protected override bool Check (int x, int y, int z)
        {
            if (!base.Check(x, y, z)) {
                return false;
            }

            int blockID = cache.Blocks.GetID(x & chunkXMask, y & chunkYMask, z & chunkZMask);

            if (!CheckBlockFilter(x, y, z, blockID))
                return false;

            if (
                ((opt.OPT_OA) && (blockID != opt.OPT_ID)) ||
                ((opt.OPT_OO) && (
                    blockID == BLOCK_COAL || blockID == BLOCK_IRON ||
                    blockID == BLOCK_GOLD || blockID == BLOCK_REDSTONE ||
                    blockID == BLOCK_DIAMOND || blockID == BLOCK_LAPIS ||
                    blockID == BLOCK_DIRT || blockID == BLOCK_GRAVEL) && (blockID != opt.OPT_ID)) ||
                (opt.OPT_OB_INCLUDE.Count > 0) ||
                (blockID == BLOCK_STONE)
            ) {
                // If overriding list of ores, check membership
                if (opt.OPT_OB_INCLUDE.Count > 0 && !opt.OPT_OB_INCLUDE.Contains(blockID)) {
                    return false;
                }

                // Check for any excluded block
                if (opt.OPT_OB_EXCLUDE.Contains(blockID)) {
                    return false;
                }

                // We're allowed to update the block
                return true;
            }

            return false;
        }

        private bool CheckBlockFilter (int x, int y, int z, int blockId)
        {
            IBlockFilter opt_b = opt.GetBlockFilter();

            if (!opt_b.InvertXYZ) {
                if (opt_b.XAboveEq != null && x < opt_b.XAboveEq)
                    return false;
                if (opt_b.XBelowEq != null && x > opt_b.XBelowEq)
                    return false;
                if (opt_b.YAboveEq != null && y < opt_b.YAboveEq)
                    return false;
                if (opt_b.YBelowEq != null && y > opt_b.YBelowEq)
                    return false;
                if (opt_b.ZAboveEq != null && z < opt_b.ZAboveEq)
                    return false;
                if (opt_b.ZBelowEq != null && z > opt_b.ZBelowEq)
                    return false;
            }
            else {
                if (opt_b.XAboveEq != null && opt_b.XBelowEq != null &&
                    opt_b.YAboveEq != null && opt_b.YBelowEq != null &&
                    opt_b.ZAboveEq != null && opt_b.ZBelowEq != null &&
                    x > opt_b.XAboveEq && x < opt_b.XBelowEq &&
                    y > opt_b.YAboveEq && y < opt_b.YBelowEq &&
                    z > opt_b.ZAboveEq && z < opt_b.ZBelowEq)
                    return false;
            }

            if (opt_b.IncludedBlockCount > 0 & !opt_b.IncludedBlocksContains(blockId))
                return false;
            if (opt_b.ExcludedBlockCount > 0 & opt_b.ExcludedBlocksContains(blockId))
                return false;

            if (opt_b.BlocksAboveCount > 0 && y < chunkYDim - 1) {
                int neighborId = cache.Blocks.GetID(x & chunkXMask, (y + 1) & chunkYMask, z & chunkZMask);
                if (!opt_b.BlocksAboveContains(neighborId))
                    return false;
            }

            if (opt_b.BlocksBelowCount > 0 && y > 0) {
                int neighborId = cache.Blocks.GetID(x & chunkXMask, (y - 1) & chunkYMask, z & chunkZMask);
                if (!opt_b.BlocksBelowContains(neighborId))
                    return false;
            }

            if (opt_b.BlocksSideCount > 0) {
                while (true) {
                    AlphaBlockRef block1 = GetBlockRefUnchecked(x - 1, y, z);
                    if (block1.IsValid && opt_b.BlocksSideContains(block1.ID))
                        break;
                    AlphaBlockRef block2 = GetBlockRefUnchecked(x + 1, y, z);
                    if (block2.IsValid && opt_b.BlocksSideContains(block2.ID))
                        break;
                    AlphaBlockRef block3 = GetBlockRefUnchecked(x, y, z - 1);
                    if (block3.IsValid && opt_b.BlocksSideContains(block3.ID))
                        break;
                    AlphaBlockRef block4 = GetBlockRefUnchecked(x, y, z + 1);
                    if (block4.IsValid && opt_b.BlocksSideContains(block4.ID))
                        break;
                    return false;
                }
            }

            if (opt_b.BlocksNAboveCount > 0 && y < chunkYDim - 1) {
                int neighborId = cache.Blocks.GetID(x & chunkXMask, (y + 1) & chunkYMask, z & chunkZMask);
                if (opt_b.BlocksNAboveContains(neighborId))
                    return false;
            }

            if (opt_b.BlocksNBelowCount > 0 && y > 0) {
                int neighborId = cache.Blocks.GetID(x & chunkXMask, (y - 1) & chunkYMask, z & chunkZMask);
                if (opt_b.BlocksNBelowContains(neighborId))
                    return false;
            }

            if (opt_b.BlocksNSideCount > 0) {
                while (true) {
                    AlphaBlockRef block1 = GetBlockRefUnchecked(x - 1, y, z);
                    if (block1.IsValid && opt_b.BlocksNSideContains(block1.ID))
                        return false;
                    AlphaBlockRef block2 = GetBlockRefUnchecked(x + 1, y, z);
                    if (block2.IsValid && opt_b.BlocksNSideContains(block2.ID))
                        return false;
                    AlphaBlockRef block3 = GetBlockRefUnchecked(x, y, z - 1);
                    if (block3.IsValid && opt_b.BlocksNSideContains(block3.ID))
                        return false;
                    AlphaBlockRef block4 = GetBlockRefUnchecked(x, y, z + 1);
                    if (block4.IsValid && opt_b.BlocksNSideContains(block4.ID))
                        return false;
                    break;
                }
            }

            if (opt_b.ProbMatch != null) {
                double c = rand.NextDouble();
                if (c > opt_b.ProbMatch)
                    return false;
            }

            if (opt_b.IncludedDataCount > 0 || opt_b.ExcludedDataCount > 0) {
                int data = cache.Blocks.GetData(x & chunkXMask, y & chunkYMask, z & chunkZMask);
                if (opt_b.IncludedDataCount > 0 && !opt_b.IncludedDataContains(data)) {
                    return false;
                }

                if (opt_b.ExcludedDataCount > 0 && opt_b.ExcludedDataContains(data)) {
                    return false;
                }
            }

            return true;
        }

        private AlphaBlockRef GetBlockRefUnchecked (int x, int y, int z)
        {
            ChunkRef cache = GetChunk(x, y, z);
            if (cache == null) {
                return new AlphaBlockRef();
            }

            return cache.Blocks.GetBlockRef(x & chunkXMask, y & chunkYMask, z & chunkZMask);
        }
    }
}
