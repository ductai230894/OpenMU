﻿// <copyright file="MagicEffectsList.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.GameLogic
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The list of magic effects of a player instance. Automatically applies the powerups of the effects to the player.
    /// </summary>
    public class MagicEffectsList
    {
        private readonly BitArray contains = new BitArray(0x100);
        private readonly Player owner;
        private readonly object addLock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="MagicEffectsList"/> class.
        /// </summary>
        /// <param name="player">The player.</param>
        public MagicEffectsList(Player player)
        {
            this.owner = player;
            this.ActiveEffects = new Dictionary<byte, MagicEffect>(6);
        }

        /// <summary>
        /// Gets the active effects.
        /// </summary>
        public IDictionary<byte, MagicEffect> ActiveEffects { get; }

        /// <summary>
        /// Adds the effect and applies the power ups.
        /// </summary>
        /// <param name="effect">The effect.</param>
        public void AddEffect(MagicEffect effect)
        {
            bool added = false;
            lock (this.addLock)
            {
                if (this.contains[effect.Id])
                {
                    this.UpdateEffect(effect);
                }
                else
                {
                    added = true;
                    this.ActiveEffects.Add(effect.Id, effect);
                    this.contains[effect.Id] = true;
                    foreach (var powerUp in effect.PowerUpElements)
                    {
                        this.owner.Attributes.AddElement(powerUp.Element, powerUp.Target);
                    }
                }
            }

            if (added)
            {
                effect.EffectTimeOut += this.OnEffectTimeOut;
                this.owner.PlayerView.ActivateMagicEffect(effect, this.owner);
                if (effect.Definition.InformObservers)
                {
                    this.owner.ForEachObservingPlayer(p => p.PlayerView.ActivateMagicEffect(effect, this.owner), false);
                }
            }
        }

        /// <summary>
        /// Gets the active visible effect ids.
        /// </summary>
        /// <returns>An array of active effect ids.</returns>
        public IList<MagicEffect> GetVisibleEffects()
        {
            return this.ActiveEffects.Values.Where(me => me.Definition.InformObservers).ToList();
        }

        /// <summary>
        /// Clears all active effects.
        /// </summary>
        public void ClearAllEffects()
        {
            while (this.ActiveEffects.Any())
            {
                this.ActiveEffects.Values.First().Dispose();
            }
        }

        private void OnEffectTimeOut(object sender, EventArgs args)
        {
            var effect = sender as MagicEffect;

            lock (this.addLock)
            {
                this.ActiveEffects.Remove(effect.Id);
                this.contains[effect.Id] = false;
            }

            foreach (var powerUp in effect.PowerUpElements)
            {
                this.owner.Attributes.RemoveElement(powerUp.Element, powerUp.Target);
            }

            this.owner.PlayerView.DeactivateMagicEffect(effect, this.owner);
            if (effect.Definition.InformObservers)
            {
                this.owner.ForEachObservingPlayer(p => p.PlayerView.DeactivateMagicEffect(effect, this.owner), false);
            }
        }

        /// <summary>
        /// Updates the effect.
        /// </summary>
        /// <param name="effect">The effect.</param>
        private void UpdateEffect(MagicEffect effect)
        {
            MagicEffect magicEffect = this.ActiveEffects[effect.Id];
            if (magicEffect.Value > effect.Value)
            {
                // no debuffing allowed
                return;
            }

            //// GMO behaviour would be: RemoveEffect(magicEffect.Id); AddEffect(effect);
            //// I change the existing Timer and Buff Value, without removing the effect itself.
            //// This doesnt only save traffic, it also looks better in game.
            magicEffect.Duration = effect.Duration;
            magicEffect.ResetTimer();
            foreach (var powerUp in magicEffect.PowerUpElements)
            {
                this.owner.Attributes.RemoveElement(powerUp.Element, powerUp.Target);
            }

            magicEffect.PowerUpElements = effect.PowerUpElements;
            foreach (var powerUp in magicEffect.PowerUpElements)
            {
                this.owner.Attributes.AddElement(powerUp.Element, powerUp.Target);
            }
        }
    }
}
