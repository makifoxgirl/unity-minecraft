using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity_Maki_Space.Scripts.Managers
{
    public class DependencyManager : MonoBehaviour
    {
        public static DependencyManager Instance;

        public TextureManager TextureManager;

        public ChunkMaterialManager ChunkMaterialManager;

        [Header("UI Manager")] public Canvas uiCanvas;
        public UIManager UIManager;

        public List<GameObject> persistantGameObjects;
        
        private Manager[] managers;
        private bool initialized;

        private async void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            var newManagers = new List<Manager>();

            newManagers.Add(TextureManager = new TextureManager());

            newManagers.Add(ChunkMaterialManager = new ChunkMaterialManager());
            
            newManagers.Add(UIManager = new UIManager(uiCanvas));
            persistantGameObjects.Add(uiCanvas.gameObject);

            managers = newManagers.ToArray();
            
            foreach (var persistantGameObject in persistantGameObjects)
            {
                DontDestroyOnLoad(persistantGameObject);
            }
            
            // do scene change here if necessary

            await Task.WhenAll(managers.Select(m => m.Init()));

            initialized = true;
        }

        private void Update()
        {
            if (!initialized) return;
            foreach (var manager in managers)
            {
                manager.Update();
            }
        }

        private void OnDestroy()
        {
            if (!initialized) return;
            foreach (var manager in managers)
            {
                manager.Update();
            }
        }
    }
}