using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HappyHarvest
{
    [RequireComponent(typeof(Collider2D))]
    public class SceneTransition : MonoBehaviour
    {
        public int TargetSceneBuildIndex;
        public int TargetSpawnIndex;

        private void OnTriggerEnter2D(Collider2D col)
        {
            // Seeds of Empathy: con dos personajes, solo el que controla el jugador abre la transición.
            // (El hijo caminando solo durante una crisis, o su zona de mediación, no deben cambiar de escena.)
            var character = col.GetComponentInParent<PlayerController>();
            if (character != null && character != GameManager.Instance.Player)
                return;

            GameManager.Instance.MoveTo(TargetSceneBuildIndex, TargetSpawnIndex);
        }
    }
}