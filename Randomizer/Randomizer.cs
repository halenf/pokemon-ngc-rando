﻿using Randomizer.Shufflers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XDCommon.PokemonDefinitions;
using XDCommon.Utility;

namespace Randomizer
{
    public enum PRNGChoice { Net, Cryptographic, Xoroshiro128 }
    public class Randomizer : IDisposable
    {
        AbstractRNG random;
        IGameExtractor gameExtractor;
        private bool disposedValue;

        ShuffleSettings shuffleSettings;

        public Randomizer(IGameExtractor extractor, Settings settings, PRNGChoice prng, int seed = -1)
        {
            shuffleSettings = new ShuffleSettings
            {
                ExtractedGame = new ExtractedGame(extractor),
                RandomizerSettings = settings,
                RNG = prng switch
                {
                    PRNGChoice.Net => new NetRandom(seed),
                    PRNGChoice.Cryptographic => new Cryptographic(),
                    _ => new Xoroshiro128StarStar(seed > 0 ? (ulong)seed : 0)
                }
            };
            gameExtractor = extractor;
        }

        public void RandomizeMoves()
        {
            MoveShuffler.RandomizeMoves(shuffleSettings);
        }

        public void RandomizePokemonTraits()
        {
            PokemonTraitShuffler.RandomizePokemonTraits(shuffleSettings);
        }

        public int[] RandomizeTrainers()
        {
            return TeamShuffler.ShuffleTeams(shuffleSettings);
        }

        public void RandomizeItems()
        {
            ItemShuffler.ShuffleTMs(shuffleSettings);
            ItemShuffler.ShuffleOverworldItems(shuffleSettings);
            ItemShuffler.UpdatePokemarts(shuffleSettings);

            if (gameExtractor is XDExtractor xd)
            {
                ItemShuffler.ShuffleTutorMoves(shuffleSettings);
            }
        }

        public void RandomizeStatics(int[] pickedShadows)
        {
            if (gameExtractor is XDExtractor xd)
            {
                var starter = xd.GetStarter();
                StaticPokemonShuffler.RandomizeXDStatics(shuffleSettings, starter, gameExtractor.ISO, pickedShadows);
            }
            else if (gameExtractor is ColoExtractor colo)
            {
                var starters = colo.GetStarters();
                StaticPokemonShuffler.RandomizeColoStatics(shuffleSettings, starters, pickedShadows);
            }
        }

        public void RandomizeBattleBingo()
        {
            if (gameExtractor is XDExtractor xd)
            {
                var bCards = xd.ExtractBattleBingoCards();
                BingoCardShuffler.ShuffleCards(shuffleSettings, bCards);
            }
        }

        public void RandomizePokeSpots()
        {
            if (gameExtractor is XDExtractor xd)
            {
                var pokespots = xd.ExtractPokeSpotPokemon();
                PokeSpotShuffler.ShufflePokeSpots(shuffleSettings, pokespots);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    gameExtractor = null;
                }

                if (random is Cryptographic c)
                {
                    c.Dispose();
                }
                random = null;
                disposedValue = true;
            }
        }

        ~Randomizer()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
