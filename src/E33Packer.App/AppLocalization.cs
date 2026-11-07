using System.Globalization;
using E33ModEditor.Core.Localization;

namespace E33Packer.App;

/// <summary>
/// UI strings for the four supported languages. English strings double as the
/// lookup keys; missing translations fall back to English.
/// </summary>
internal static class AppLocalization
{
    public static string Current { get; private set; } = "en";

    public static void SetLanguage(string? code) =>
        Current = ModLocalization.Find(code)?.Code ?? "en";

    public static string T(string english) => TFor(Current, english);

    public static string TFor(string? code, string english)
    {
        var language = ModLocalization.Find(code)?.Code ?? "en";
        if (language == "en") return english;
        return Table.TryGetValue(language, out var map) &&
               map.TryGetValue(english, out var translated)
            ? translated
            : english;
    }

    public static string F(string english, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, T(english), args);

    public static string NativeSubtitle(string code) => code switch
    {
        "fr" => "Langue du jeu : français",
        "pl" => "Język gry: polski",
        "ru" => "Язык игры: русский",
        _ => "English",
    };

    public static string PresetName(string canonical) => T(canonical);

    public static string PresetDescription(string canonical) => canonical switch
    {
        "Easy" => T("Enemy stat bonuses are 25% weaker than the Expedition Infinity preset; normal defense windows. Includes instant Picto mastery."),
        "Gentler" => T("Enemy stat bonuses are 25% weaker than the Expedition Infinity preset; normal defense windows. Includes instant Picto mastery."),
        "Brutal" => T("Enemy HP bonus is doubled and speed/attack bonuses are 50% stronger than the Expedition Infinity preset; half Lumina."),
        "Custom" => T("Your own custom settings."),
        _ => T("Standard Expedition Infinity combat and progression."),
    };

    public static string Stage(E33ModEditor.Core.Packer.BuildStage stage) => stage switch
    {
        E33ModEditor.Core.Packer.BuildStage.Validating => T("Checking settings"),
        E33ModEditor.Core.Packer.BuildStage.Tools => T("Preparing tools"),
        E33ModEditor.Core.Packer.BuildStage.Donors => T("Preparing game data"),
        E33ModEditor.Core.Packer.BuildStage.Core => T("Building core content"),
        E33ModEditor.Core.Packer.BuildStage.Balance => T("Applying balance"),
        E33ModEditor.Core.Packer.BuildStage.QualityOfLife => T("Applying quality of life"),
        E33ModEditor.Core.Packer.BuildStage.Randomizer => T("Rolling the seed"),
        E33ModEditor.Core.Packer.BuildStage.Localization => T("Applying language"),
        E33ModEditor.Core.Packer.BuildStage.Spoiler => T("Writing the spoiler"),
        E33ModEditor.Core.Packer.BuildStage.Packing => T("Packing the seed"),
        E33ModEditor.Core.Packer.BuildStage.Verifying => T("Checking the pack"),
        E33ModEditor.Core.Packer.BuildStage.Installing => T("Installing"),
        E33ModEditor.Core.Packer.BuildStage.Done => T("Done"),
        _ => stage.ToString(),
    };

    private static readonly Dictionary<string, Dictionary<string, string>> Table =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["fr"] = new(StringComparer.Ordinal)
            {
                ["Randomizer"] = "Aléatoire",
                ["Balance"] = "Équilibre",
                ["Quality of Life"] = "Qualité de vie",
                ["Language"] = "Langue",
                ["Loot sources"] = "Sources de butin",
                ["Install folder (created if missing)"] = "Dossier d'installation (créé si nécessaire)",
                ["Expedition 33 game folder"] = "Dossier du jeu Expedition 33",
                ["Changes the pack and app language."] = "Change la langue du pack et de l'application.",
                ["Language of the pack and the app."] = "Langue du pack et de l'application.",
                ["Checking which game files are needed…"] = "Vérification des fichiers du jeu nécessaires…",
                ["Game data ready."] = "Données du jeu prêtes.",
                ["Game data ready — {0} extraction task(s) completed."] = "Données du jeu prêtes — {0} tâche(s) d'extraction terminée(s).",
                ["Game files not found in that folder."] = "Fichiers du jeu introuvables dans ce dossier.",
                ["Install folder is not writable."] = "Le dossier d'installation n'est pas accessible en écriture.",
                ["New seed"] = "Nouvelle graine",
                ["Profile loaded."] = "Profil chargé.",
                ["Profile saved."] = "Profil enregistré.",
                ["Restored seed {0}."] = "Graine {0} restaurée.",
                ["Loading…"] = "Chargement…",
                ["Could not open the generator."] = "Impossible d'ouvrir le générateur.",
                ["Pack: {0}"] = "Pack : {0}",
                ["× (0.5 = half)"] = "× (0,5 = moitié)",
                ["Settings validation failed."] = "La validation des réglages a échoué.",
                ["Starting…"] = "Démarrage…",
                ["This one-time setup reads game data. Later builds skip it."] = "Cette configuration unique lit les données du jeu. Les créations suivantes l'ignorent.",
                ["vanilla 0.15"] = "origine 0,15",
                ["vanilla 0.22"] = "origine 0,22",
                ["Packed {0} ({1} edits) into {2}"] = "Pack {0} créé ({1} modifications) dans {2}",
                ["Packed {0} ({1} edits); library {2}; installed in {3}"] = "Pack {0} créé ({1} modifications) ; bibliothèque {2} ; installé dans {3}",
                ["Max AP 21 · 3 consecutive turns · max Foretell 30 · max shields 5 · burn 0.075 / 0.01 / 3 turns · crit addend 1.5 with crit-rate scaling. Use the full editor for experimental changes."] =
                    "AP max 21 · 3 tours consécutifs · Prévision max 30 · boucliers max 5 · brûlure 0,075 / 0,01 / 3 tours · bonus critique 1,5 avec mise à l'échelle du taux critique. Utilisez l'éditeur complet pour les changements expérimentaux.",
                ["Build a seed pack for Expedition Infinity."] = "Créez un pack de graine pour Expedition Infinity.",
                ["Core movesets, pictos and weapons are always included. The seed controls loot, stats and encounters."] =
                    "Les compétences, pictos et armes de base sont toujours inclus. La graine contrôle le butin, les statistiques et les rencontres.",
                ["Setup"] = "Configuration",
                ["Game folder, output folder and profile."] = "Dossier du jeu, dossier d'installation et profil.",
                ["Game folder"] = "Dossier du jeu",
                ["The folder that contains the \"Sandfall\" folder."] = "Le dossier qui contient le dossier « Sandfall ».",
                ["Usually found automatically. Browse only if needed."] = "Généralement trouvé automatiquement. Parcourir seulement si nécessaire.",
                ["Choose the game folder manually."] = "Choisir manuellement le dossier du jeu.",
                ["Find automatically"] = "Trouver automatiquement",
                ["Search the usual Steam folders."] = "Rechercher dans les dossiers Steam habituels.",
                ["Install folder"] = "Dossier d'installation",
                ["Where the finished seed pack is installed. The game reads it from its ~mods folder."] =
                    "Destination du pack de graine. Le jeu le lit depuis son dossier ~mods.",
                ["Defaults to the game's ~mods folder. Created if missing."] =
                    "Par défaut, le dossier ~mods du jeu. Créé si nécessaire.",
                ["Choose a different install folder."] = "Choisir un autre dossier d'installation.",
                ["Use default"] = "Utiliser par défaut",
                ["Use the game's standard mod folder (~mods)."] = "Utiliser le dossier de mods standard du jeu (~mods).",
                ["Profile"] = "Profil",
                ["Save or load your seed settings."] = "Enregistrer ou charger vos réglages de graine.",
                ["Save profile…"] = "Enregistrer le profil…",
                ["Save these settings so you can reuse them."] = "Enregistrer ces réglages pour les réutiliser.",
                ["Load profile…"] = "Charger le profil…",
                ["Load a saved settings file."] = "Charger un fichier de réglages enregistré.",
                ["Browse…"] = "Parcourir…",
                ["Seed"] = "Graine",
                ["Same seed and profile give the same results."] = "Même graine et même profil donnent les mêmes résultats.",
                ["Copy this seed to recreate the same pack later."] = "Copiez cette graine pour recréer le même pack plus tard.",
                ["Random"] = "Aléatoire",
                ["Pick a new random seed."] = "Choisir une nouvelle graine aléatoire.",
                ["Copy"] = "Copier",
                ["Copy the seed."] = "Copier la graine.",
                ["Build seed pack"] = "Créer le pack de graine",
                ["Build the pack and install it into the game's ~mods folder."] =
                    "Créer le pack et l'installer dans le dossier ~mods du jeu.",
                ["Cancel"] = "Annuler",
                ["Cancel between build stages; the installed pack is not replaced."] =
                    "Annuler entre les étapes ; le pack installé n'est pas remplacé.",
                ["Open install folder"] = "Ouvrir le dossier d'installation",
                ["Open the game's ~mods folder."] = "Ouvrir le dossier ~mods du jeu.",
                ["Open app folder"] = "Ouvrir le dossier de l'application",
                ["Open the application folder that stores seeds and caches."] =
                    "Ouvrir le dossier de l'application qui contient les graines et les caches.",
                ["Open seed library"] = "Ouvrir la bibliothèque de graines",
                ["Open the folder that keeps every generated seed."] =
                    "Ouvrir le dossier qui conserve toutes les graines générées.",
                ["Copy log"] = "Copier le journal",
                ["Copy the build log for troubleshooting."] = "Copier le journal de création pour le dépannage.",
                ["Clear log"] = "Effacer le journal",
                ["Clear the log window."] = "Effacer la fenêtre du journal.",
                ["Ready."] = "Prêt.",
                ["Idle."] = "Inactif.",
                ["Game folder not found."] = "Dossier du jeu introuvable.",
                ["Game folder ready."] = "Dossier du jeu prêt.",
                ["That folder does not look like Expedition 33."] =
                    "Ce dossier ne ressemble pas à Expedition 33.",
                ["Expedition 33 found. Confirm the folder."] =
                    "Expedition 33 trouvé. Confirmez le dossier.",
                ["The application folder is not writable."] =
                    "Le dossier de l'application n'est pas accessible en écriture.",
                ["Build cancelled before extraction."] = "Création annulée avant l'extraction.",
                ["Build cancelled. The previously installed pack was not replaced."] =
                    "Création annulée. Le pack précédemment installé n'a pas été remplacé.",
                ["Pack failed."] = "Échec de la création du pack.",
                ["Building. Quit the game completely before packing."] =
                    "Création en cours. Fermez complètement le jeu avant de créer le pack.",
                ["Building…"] = "Création…",
                ["Extracting…"] = "Extraction…",
                ["Cancelling after the current stage…"] = "Annulation après l'étape en cours…",
                ["Cancelling build; please wait."] = "Annulation de la création ; veuillez patienter.",
                ["Preparing game data"] = "Préparation des données du jeu",
                ["Extracting game data. This can take several minutes; keep the app open."] =
                    "Extraction des données du jeu. Cela peut prendre plusieurs minutes ; gardez l'application ouverte.",
                ["Game data ready"] = "Données du jeu prêtes",
                ["Game data extraction failed."] = "Échec de l'extraction des données du jeu.",
                ["Game data extraction skipped. The first build will ask again."] =
                    "Extraction ignorée. La première création redemandera.",
                ["Language changed."] = "Langue modifiée.",
                ["Type a seed or roll a new one."] = "Saisissez une graine ou générez-en une nouvelle.",
                ["Type a number from 0 to 99,999,999."] = "Saisissez un nombre de 0 à 99 999 999.",
                ["Pick a random seed."] = "Choisir une graine aléatoire.",
                ["Name of the pack that will be generated."] = "Nom du pack qui sera généré.",
                ["Your last 20 builds."] = "Vos 20 dernières créations.",
                ["Seed history"] = "Historique des graines",
                ["Recent builds, newest first."] = "Créations récentes, les plus récentes d'abord.",
                ["Restore seed + profile"] = "Restaurer graine + profil",
                ["Reload the seed and all settings from the selected build."] =
                    "Recharger la graine et tous les réglages de la création sélectionnée.",
                ["The seed controls loot, stats and encounters. Core content is always included."] =
                    "La graine contrôle le butin, les statistiques et les rencontres. Le contenu de base est toujours inclus.",
                ["Choose what the seed can change. Keys, journals, music and quest items stay vanilla."] =
                    "Choisissez ce que la graine peut modifier. Les clés, journaux, musiques et objets de quête restent d'origine.",
                ["Chests and pickups"] = "Coffres et objets ramassables",
                ["Pictos and weapons found in chests and pickups."] =
                    "Pictos et armes trouvés dans les coffres et les objets ramassables.",
                ["Paint Cages"] = "Cages de peinture",
                ["Each cage rolls one tier-appropriate picto or weapon."] =
                    "Chaque cage donne un picto ou une arme adapté au palier.",
                ["Enemy drops"] = "Butin des ennemis",
                ["Regular, elite, boss and overworld drops."] =
                    "Butin des ennemis normaux, élites, boss et du monde ouvert.",
                ["Merchant inventories"] = "Inventaires des marchands",
                ["Tier-appropriate shop stock."] = "Stock adapté au palier.",
                ["Weapon passives"] = "Passifs d'armes",
                ["Rolls the passive effect on weapons."] = "Tire l'effet passif des armes.",
                ["Monster stats"] = "Statistiques des monstres",
                ["Rolls enemy HP, attack, speed and defense."] =
                    "Tire les PV, l'attaque, la vitesse et la défense des ennemis.",
                ["Weapon stats and elements"] = "Statistiques et éléments des armes",
                ["Rolls weapon attack, free-aim and element."] =
                    "Tire l'attaque, le tir libre et l'élément des armes.",
                ["Battle groups"] = "Groupes de combat",
                ["Adds extra enemies to regular encounters. Experimental."] =
                    "Ajoute des ennemis aux rencontres normales. Expérimental.",
                ["Re-roll encounters"] = "Relancer les rencontres",
                ["Picks a fresh encounter each time a battle starts. Experimental."] =
                    "Choisit une nouvelle rencontre à chaque début de combat. Expérimental.",
                ["All off: the seed changes nothing; core content and Balance/QoL still apply."] =
                    "Tout désactivé : la graine ne change rien ; le contenu de base et les réglages restent appliqués.",
                ["Loot tuning"] = "Réglage du butin",
                ["Drop rates, shop stock and experimental battle groups."] =
                    "Taux de butin, stock des marchands et groupes de combat expérimentaux.",
                ["Drop rates and shop stock."] = "Taux de butin et stock des marchands.",
                ["Regular enemy pictos chance"] = "Chance de picto sur ennemi normal",
                ["Chance a regular enemy drops a picto."] = "Chance qu'un ennemi normal donne un picto.",
                ["Elite enemy pictos chance"] = "Chance de picto sur ennemi élite",
                ["Chance an elite enemy drops a picto."] = "Chance qu'un ennemi élite donne un picto.",
                ["Perfect catalyst chance"] = "Chance de catalyseur parfait",
                ["Rare endgame catalyst. 3.125% is about 1 in 32. Set 0 to disable."] =
                    "Catalyseur rare de fin de jeu. 3,125 % correspond à environ 1 sur 32. Mettez 0 pour désactiver.",
                ["Petank Colour of Lumina"] = "Couleur de Lumina du Petank",
                ["Lumina awarded by a Petank."] = "Lumina accordée par un Petank.",
                ["Merchant pictos + weapon slots"] = "Emplacements pictos + armes des marchands",
                ["Extra gear slots in shops."] = "Emplacements d'équipement supplémentaires en boutique.",
                ["Tint shards per type (T1–T4)"] = "Éclats de teinte par type (T1–T4)",
                ["Tint shards found in early and mid-game chests."] =
                    "Éclats de teinte trouvés dans les coffres du début et du milieu du jeu.",
                ["Chroma Elixir shards (T1–T4)"] = "Éclats d'élixir chroma (T1–T4)",
                ["Chroma Elixir shards found in early and mid-game chests."] =
                    "Éclats d'élixir chroma trouvés dans les coffres du début et du milieu du jeu.",
                ["Shapes per type (T1–T4)"] = "Formes par type (T1–T4)",
                ["Shapes found in early and mid-game chests."] =
                    "Formes trouvées dans les coffres du début et du milieu du jeu.",
                ["Max enemies per battle"] = "Ennemis maximum par combat",
                ["Upper limit for battle groups. Bosses and scripted fights are excluded."] =
                    "Limite des groupes de combat. Les boss et combats scénarisés sont exclus.",
                ["Battle groups area"] = "Zone des groupes de combat",
                ["Flying Waters (test area)"] = "Eaux volantes (zone de test)",
                ["All eligible areas"] = "Toutes les zones éligibles",
                ["Limit battle groups to the test area or allow every eligible encounter."] =
                    "Limiter les groupes à la zone de test ou autoriser toutes les rencontres éligibles.",
                ["Chance to add enemies"] = "Chance d'ajouter des ennemis",
                ["How often a regular encounter gets extra enemies."] =
                    "Fréquence à laquelle une rencontre normale reçoit des ennemis supplémentaires.",
                ["Shuffle final battle rosters"] = "Mélanger les groupes de combat finaux",
                ["Randomize the order of enemies in the final group."] =
                    "Randomiser l'ordre des ennemis du groupe final.",
                ["Variants per encounter"] = "Variantes par rencontre",
                ["How many alternate groups are prepared for each encounter."] =
                    "Nombre de groupes alternatifs préparés pour chaque rencontre.",
                ["Enemy stat bands"] = "Plages de statistiques ennemies",
                ["Multipliers per tier. 1.0 is vanilla."] = "Multiplicateurs par palier. 1,0 correspond à l'origine.",
                ["Enemy stat min"] = "Statistique ennemie min.",
                ["Enemy stat max"] = "Statistique ennemie max.",
                ["Lowest non-boss stat multiplier for each tier."] =
                    "Multiplicateur de statistique le plus bas (hors boss) pour chaque palier.",
                ["Highest non-boss stat multiplier for each tier."] =
                    "Multiplicateur de statistique le plus élevé (hors boss) pour chaque palier.",
                ["Boss HP ×"] = "PV des boss ×",
                ["Boss HP multiplier for each tier."] = "Multiplicateur de PV des boss pour chaque palier.",
                ["Attack min"] = "Attaque min.",
                ["Attack max"] = "Attaque max.",
                ["Lowest physical and magical attack multiplier."] =
                    "Multiplicateur d'attaque physique et magique le plus bas.",
                ["Highest physical and magical attack multiplier."] =
                    "Multiplicateur d'attaque physique et magique le plus élevé.",
                ["Boss speed ×"] = "Vitesse des boss ×",
                ["Extra speed multiplier applied to bosses."] = "Multiplicateur de vitesse supplémentaire pour les boss.",
                ["Reset to Expedition preset"] = "Réinitialiser au profil Expedition",
                ["Restore the default stat bands."] = "Restaurer les plages de statistiques par défaut.",
                ["Adjust difficulty and growth after the seed rolls."] =
                    "Ajustez la difficulté et la progression après le tirage de la graine.",
                ["Enemy and lumina balance"] = "Équilibre des ennemis et des Lumina",
                ["Adjust regular enemy stats by tier"] = "Ajuster les stats des ennemis normaux par palier",
                ["Positive values strengthen regular enemies; negative values weaken them. Bosses and alphas are not affected."] =
                    "Les valeurs positives renforcent les ennemis normaux ; les valeurs négatives les affaiblissent. Les boss et alphas ne sont pas affectés.",
                ["HP adjustment T1–T5"] = "Ajustement des PV T1–T5",
                ["Added to normal enemy HP. 0% = vanilla; negative values weaken."] =
                    "Ajouté aux PV des ennemis normaux. 0 % = origine ; les valeurs négatives affaiblissent.",
                ["Speed adjustment T1–T5"] = "Ajustement de vitesse T1–T5",
                ["Added to normal enemy speed. 0% = vanilla; negative values weaken."] =
                    "Ajouté à la vitesse des ennemis normaux. 0 % = origine ; les valeurs négatives affaiblissent.",
                ["Attack adjustment T1–T5"] = "Ajustement d'attaque T1–T5",
                ["Added to normal enemy attack. 0% = vanilla; negative values weaken."] =
                    "Ajouté à l'attaque des ennemis normaux. 0 % = origine ; les valeurs négatives affaiblissent.",
                ["Reduce regular lumina drops"] = "Réduire les Lumina des ennemis normaux",
                ["Chests, enemy drops and shops only. Dialogue rewards are untouched."] =
                    "Coffres, butin et boutiques uniquement. Les récompenses de dialogue sont inchangées.",
                ["Lumina multiplier"] = "Multiplicateur de Lumina",
                ["0.5 halves regular lumina drops. Dialogue rewards are not changed."] =
                    "0,5 divise par deux les Lumina normaux. Les récompenses de dialogue sont inchangées.",
                ["Progression"] = "Progression",
                ["Level and stat growth."] = "Niveaux et progression des statistiques.",
                ["Character level cap"] = "Niveau maximum des personnages",
                ["Maximum character level."] = "Niveau maximum des personnages.",
                ["Attribute point cap"] = "Maximum de points d'attribut",
                ["Maximum points in a single attribute."] = "Nombre maximum de points dans un attribut.",
                ["XP per extra level (100+)"] = "XP par niveau supplémentaire (100+)",
                ["XP needed for each level above 99."] = "XP nécessaire pour chaque niveau au-dessus de 99.",
                ["Attribute points per level"] = "Points d'attribut par niveau",
                ["Attribute points gained per level."] = "Points d'attribut gagnés par niveau.",
                ["Skill-tree points per level"] = "Points d'arbre de compétences par niveau",
                ["Skill points gained per level."] = "Points de compétence gagnés par niveau.",
                ["Lumina points per level"] = "Points de Lumina par niveau",
                ["Lumina gained per level."] = "Lumina gagnés par niveau.",
                ["Player power"] = "Puissance du joueur",
                ["Multipliers on the normal level curves. 1.0 is intended."] =
                    "Multiplicateurs sur les courbes de niveau normales. 1,0 est la valeur prévue.",
                ["Max HP ×"] = "PV max ×",
                ["Physical Attack ×"] = "Attaque physique ×",
                ["Speed ×"] = "Vitesse ×",
                ["Critical Rate ×"] = "Taux critique ×",
                ["Defense ×"] = "Défense ×",
                ["Difficulty"] = "Difficulté",
                ["Dodge and parry timing."] = "Timing d'esquive et de parade.",
                ["Use tuned windows"] = "Utiliser les fenêtres ajustées",
                ["Shorter windows make dodging and parrying harder."] =
                    "Des fenêtres plus courtes rendent l'esquive et la parade plus difficiles.",
                ["Expedition (0.165 / 0.1125)"] = "Expedition (0,165 / 0,1125)",
                ["Tighter dodge and parry windows."] = "Fenêtres d'esquive et de parade plus courtes.",
                ["Normal (0.22 / 0.15)"] = "Normal (0,22 / 0,15)",
                ["Vanilla dodge and parry windows."] = "Fenêtres d'esquive et de parade d'origine.",
                ["Dodge window (seconds)"] = "Fenêtre d'esquive (secondes)",
                ["How long a dodge input is accepted."] = "Durée pendant laquelle une esquive est acceptée.",
                ["Parry window (seconds)"] = "Fenêtre de parade (secondes)",
                ["How long a parry input is accepted."] = "Durée pendant laquelle une parade est acceptée.",
                ["Weapon progression"] = "Progression des armes",
                ["Scale weapon drop levels"] = "Adapter le niveau des armes trouvées",
                ["Extends weapon drops beyond the vanilla level range."] =
                    "Étend les armes trouvées au-delà de la plage d'origine.",
                ["Max weapon drop level"] = "Niveau maximum des armes trouvées",
                ["Weapon drops scale from 1 to this level. Pictos stay 1–33."] =
                    "Les armes trouvées vont de 1 à ce niveau. Les pictos restent 1–33.",
                ["Fixed core tuning"] = "Réglages de base fixes",
                ["Combat basics stay at the intended Expedition Infinity values."] =
                    "Les bases du combat restent aux valeurs prévues d'Expedition Infinity.",
                ["AP, turns, burn and critical damage are fixed."] =
                    "AP, tours, brûlure et dégâts critiques sont fixes.",
                ["Convenience changes. Core balance is unchanged."] =
                    "Changements de confort. L'équilibre de base est inchangé.",
                ["Picto mastery"] = "Maîtrise des pictos",
                ["Instant Picto mastery"] = "Maîtrise immédiate des pictos",
                ["Instant mastery on pickup (always on)"] = "Maîtrise immédiate au ramassage (toujours active)",
                ["A found picto is mastered immediately. It does not grant pictos you have not found."] =
                    "Un picto trouvé est immédiatement maîtrisé. Cela ne donne pas de pictos non trouvés.",
                ["Battle proc cards"] = "Cartes de proc en combat",
                ["Max battle proc cards (0 = vanilla)"] = "Cartes de proc en combat max. (0 = origine)",
                ["Limits how many picto/passive cards can stack on screen during battle. 0 leaves it vanilla."] =
                    "Limite le nombre de cartes de picto/passif affichées pendant le combat. 0 laisse d'origine.",
                ["Tutorials and stains"] = "Tutoriels et teintes",
                ["Skip tutorials"] = "Ignorer les tutoriels",
                ["Tutorial fights are replaced by their normal enemy versions."] =
                    "Les combats de tutoriel sont remplacés par leurs versions normales.",
                ["Restore Light-stain substitution for Lune"] = "Restaurer la substitution de teinte Lumière pour Lune",
                ["A held Light stain can cover another missing element. Elemental Genesis, Cataclysm and Mayhem stay strict."] =
                    "Une teinte Lumière tenue peut couvrir un autre élément manquant. Genèse, Cataclysme et Mayhem restent stricts.",
                ["Weapon upgrades"] = "Améliorations d'armes",
                ["Bulk upgrade"] = "Amélioration groupée",
                ["Hold to upgrade multiple levels at once, up to the current catalyst tier limit."] =
                    "Maintenez pour monter plusieurs niveaux, jusqu'à la limite du palier de catalyseur actuel.",
                ["Levels per hold"] = "Niveaux par maintien",
                ["Maximum levels added by one hold."] = "Niveaux maximum ajoutés par un maintien.",
                ["Boss movesets"] = "Compétences des boss",
                ["Mask Keeper keeps the full moveset"] = "Le Gardien des masques conserve toutes ses compétences",
                ["The Mask Keeper keeps its mask moves after the sub-bosses are defeated."] =
                    "Le Gardien des masques conserve ses attaques de masque après les sous-boss.",
                ["Sirène keeps her full moveset"] = "Sirène conserve toutes ses compétences",
                ["Sirène keeps her Tisseur-synergy moves after Tisseur is defeated."] =
                    "Sirène conserve ses compétences de synergie avec Tisseur après sa défaite.",
                ["Five-member party: always on."] = "Équipe de cinq : toujours active.",
                ["Build convenience"] = "Confort de création",
                ["Open the install folder after a build"] = "Ouvrir le dossier d'installation après une création",
                ["Open the game's ~mods folder when the build finishes."] =
                    "Ouvrir le dossier ~mods du jeu à la fin de la création.",
                ["Open the spoiler after a build"] = "Ouvrir le spoiler après une création",
                ["Open the offline spoiler page in your browser."] =
                    "Ouvrir la page de spoiler hors ligne dans le navigateur.",
                ["Language of the generated pack. The app stays in English."] =
                    "Langue du pack généré et de l'application.",
                ["Choose the folder that contains the \"Sandfall\" folder."] =
                    "Choisissez le dossier qui contient le dossier « Sandfall ».",
                ["We couldn't find Expedition 33 in the usual Steam folders."] =
                    "Impossible de trouver Expedition 33 dans les dossiers Steam habituels.",
                ["Typical location: Steam → steamapps → common → Expedition 33"] =
                    "Emplacement habituel : Steam → steamapps → common → Expedition 33",
                ["Quit"] = "Quitter",
                ["Choose folder…"] = "Choisir un dossier…",
                ["Find Expedition 33"] = "Trouver Expedition 33",
                ["Expedition 33 found"] = "Expedition 33 trouvé",
                ["Use this folder"] = "Utiliser ce dossier",
                ["Choose another folder…"] = "Choisir un autre dossier…",
                ["By default, packs install to the game's ~mods folder. You can change both folders later in Setup."] =
                    "Par défaut, les packs s'installent dans le dossier ~mods du jeu. Vous pouvez modifier les deux dossiers plus tard dans Configuration.",
                ["First-time setup"] = "Configuration initiale",
                ["Finish game data setup"] = "Terminer la configuration des données",
                ["Expedition Infinity needs some data from your game before it can build seeds. This is a one-time step."] =
                    "Expedition Infinity a besoin de données de votre jeu avant de créer des graines. Cette étape est unique.",
                ["Most game data is already prepared. {0} of {1} item(s) still need to be read from your game."] =
                    "La plupart des données du jeu sont déjà prêtes. Il reste {0} élément(s) sur {1} à lire.",
                ["• About 1.2 GB of files"] = "• Environ 1,2 Go de fichiers",
                ["• Usually 5–15 minutes, depending on the drive"] = "• Généralement 5 à 15 minutes selon le disque",
                ["• Stored inside this application's folder"] = "• Stocké dans le dossier de cette application",
                ["• Quit Expedition 33 before continuing"] = "• Quittez Expedition 33 avant de continuer",
                ["• No game files are modified"] = "• Aucun fichier du jeu n'est modifié",
                ["• Only the missing items are read"] = "• Seuls les éléments manquants sont lus",
                ["• Usually quick"] = "• Généralement rapide",
                ["Saved to"] = "Enregistré dans",
                ["If you cancel, the generator will ask again before the first build or the next launch. You can still choose a seed, profile and language."] =
                    "Si vous annulez, le générateur redemandera avant la première création ou au prochain lancement. Vous pouvez toujours choisir une graine, un profil et une langue.",
                ["Start extraction"] = "Démarrer l'extraction",
                ["Finish setup"] = "Terminer la configuration",
                ["Not now"] = "Pas maintenant",
                ["Existing game data is reused; nothing is downloaded again."] =
                    "Les données existantes sont réutilisées ; rien n'est retéléchargé.",
                ["This is a one-time setup step."] = "Cette étape de configuration est unique.",
                ["Preparing game data…"] = "Préparation des données du jeu…",
                ["Choose your language"] = "Choisissez votre langue",
                ["Continue"] = "Continuer",
                ["Discard custom changes?"] = "Abandonner les modifications personnalisées ?",
                ["Loading {0} will replace your custom settings."] =
                    "Charger {0} remplacera vos réglages personnalisés.",
                ["Custom"] = "Personnalisé",
                ["Expedition Infinity"] = "Expedition Infinity",
                ["Gentler"] = "Plus doux",
                ["Brutal"] = "Brutal",
                ["Loot only"] = "Butin uniquement",
                ["Stats only"] = "Statistiques uniquement",
                ["Regular enemy stats are 25% lower than the Expedition Infinity preset; normal defense windows."] =
                    "Les stats des ennemis normaux sont 25 % inférieures au profil Expedition Infinity ; fenêtres de défense normales.",
                ["Enemy HP is doubled and speed/attack are 50% higher than the Expedition Infinity preset; half lumina."] =
                    "Les PV des ennemis sont doublés et leur vitesse/attaque sont 50 % supérieures au profil Expedition Infinity ; moitié moins de Lumina.",
                ["Standard combat; only loot rolls change."] =
                    "Combat standard ; seuls les tirages de butin changent.",
                ["Standard combat; only enemy and weapon stats change."] =
                    "Combat standard ; seules les stats des ennemis et des armes changent.",
                ["Your own custom settings."] = "Vos propres réglages personnalisés.",
                ["Standard Expedition Infinity combat and progression."] =
                    "Combat et progression Expedition Infinity standard.",
                ["Checking settings"] = "Vérification des réglages",
                ["Preparing tools"] = "Préparation des outils",
                ["Building core content"] = "Construction du contenu de base",
                ["Applying balance"] = "Application de l'équilibre",
                ["Applying quality of life"] = "Application de la qualité de vie",
                ["Rolling the seed"] = "Tirage de la graine",
                ["Applying language"] = "Application de la langue",
                ["Writing the spoiler"] = "Écriture du spoiler",
                ["Packing the seed"] = "Empaquetage de la graine",
                ["Checking the pack"] = "Vérification du pack",
                ["Installing"] = "Installation",
                ["Done"] = "Terminé",
                ["enemy data"] = "données des ennemis",
                ["weapon and gear data"] = "données des armes et équipements",
                ["skill data"] = "données des compétences",
                ["picto data"] = "données des pictos",
                ["loot and merchant data"] = "données du butin et des marchands",
                ["text and localization"] = "textes et localisation",
                ["battle data"] = "données de combat",
                ["character data"] = "données des personnages",
                ["game blueprints"] = "plans du jeu",
                ["game data assets"] = "ressources du jeu",
                ["game assets"] = "ressources du jeu",
                ["game data"] = "données du jeu",
                ["Extracting {0}…"] = "Extraction : {0}…",
                ["{0} of {1} tasks complete ({2}%)"] = "{0} sur {1} tâches terminées ({2} %)",
                ["This is a one-time setup. Keep this window open."] =
                    "Cette configuration est unique. Gardez cette fenêtre ouverte.",
                ["That folder does not look like Expedition 33. Choose the folder that contains the \"Sandfall\" folder."] =
                    "Ce dossier ne ressemble pas à Expedition 33. Choisissez le dossier qui contient « Sandfall ».",
                ["Choose the Expedition 33 game folder"] = "Choisir le dossier du jeu Expedition 33",
                ["Choose the install folder"] = "Choisir le dossier d'installation",
                ["Save Expedition Infinity profile"] = "Enregistrer le profil Expedition Infinity",
                ["Load Expedition Infinity profile"] = "Charger le profil Expedition Infinity",
                ["Expedition Infinity profile"] = "Profil Expedition Infinity",
                ["The seed controls loot, Nevron stats and encounters. Core content is always included."] =
                    "La graine contrôle le butin, les stats des Nevrons et les rencontres. Le contenu de base est toujours inclus.",
                ["Loot"] = "Butin",
                ["Loot options"] = "Options de butin",
                ["Nevrons"] = "Nevrons",
                ["Nevron options"] = "Options des Nevrons",
                ["Choose which loot sources the seed changes. Keys, journals, music and quest items stay vanilla."] =
                    "Choisissez les sources de butin modifiées par la graine. Les clés, journaux, musiques et objets de quête restent d'origine.",
                ["Nevron drops"] = "Butin des Nevrons",
                ["Regular, elite, boss and overworld Nevron drops."] =
                    "Butin des Nevrons normaux, élites, boss et du monde ouvert.",
                ["Drop rates, weapon rolls and loot quantities."] = "Taux de butin, tirages d'armes et quantités.",
                ["Regular Nevron picto chance"] = "Chance de picto sur Nevron normal",
                ["Chance a regular Nevron drops a picto."] = "Chance qu'un Nevron normal donne un picto.",
                ["Elite Nevron picto chance"] = "Chance de picto sur Nevron élite",
                ["Chance an elite Nevron drops a picto."] = "Chance qu'un Nevron élite donne un picto.",
                ["Chests, Nevron drops and shops only. Dialogue rewards are untouched."] =
                    "Coffres, butin des Nevrons et boutiques uniquement. Les récompenses de dialogue sont inchangées.",
                ["Choose whether Nevron stats are rerolled by the seed."] =
                    "Choisissez si les stats des Nevrons sont retirées par la graine.",
                ["Nevron stats"] = "Stats des Nevrons",
                ["Rolls Nevron HP, attack, speed and defense."] =
                    "Tire les PV, l'attaque, la vitesse et la défense des Nevrons.",
                ["Turned off: the seed leaves Nevron stats at their rolled band defaults."] =
                    "Désactivé : la graine laisse les stats des Nevrons à leurs valeurs de bande.",
                ["Rolled stat bands, followed by an optional extra adjustment."] =
                    "Bandes de stats tirées, puis un ajustement supplémentaire facultatif.",
                ["Rolled bands"] = "Bandes tirées",
                ["Extra adjustment"] = "Ajustement supplémentaire",
                ["Nevron stat min"] = "Stat Nevron min.",
                ["Nevron stat max"] = "Stat Nevron max.",
                ["Reset bands to Expedition preset"] = "Réinitialiser les bandes au profil Expedition",
                ["Restore the default Nevron stat bands."] = "Restaurer les bandes de stats Nevron par défaut.",
                ["Adjust regular Nevron stats by tier"] = "Ajuster les stats des Nevrons normaux par palier",
                ["Positive values strengthen regular Nevrons; negative values weaken them. Bosses and alphas are not affected."] =
                    "Les valeurs positives renforcent les Nevrons normaux ; les valeurs négatives les affaiblissent. Les boss et alphas ne sont pas affectés.",
                ["Added after the roll. 0% = no extra adjustment."] =
                    "Ajouté après le tirage. 0 % = aucun ajustement supplémentaire.",
                ["All sources off: the seed changes nothing; core content and Balance/QoL still apply."] =
                    "Toutes sources désactivées : la graine ne change rien ; le contenu de base et les réglages restent appliqués.",
                ["Adjust progression, attributes, boss movesets and parry timing after the seed rolls."] =
                    "Ajustez la progression, les attributs, les compétences des boss et le timing de parade après le tirage.",
                ["Attributes"] = "Attributs",
                ["Multipliers on the character's attribute curves. 1.0 is intended."] =
                    "Multiplicateurs sur les courbes d'attributs. 1,0 est la valeur prévue.",
                ["Parry timings"] = "Fenêtres de parade",
                ["Dodge and parry windows."] = "Fenêtres d'esquive et de parade.",
                ["These values are fixed in the generator."] = "Ces valeurs sont fixes dans le générateur.",
                ["Easy"] = "Facile",
                ["Enemy stat bonuses are 25% weaker than the Expedition Infinity preset; normal defense windows. Includes instant Picto mastery."] =
                    "Les bonus de stats des Nevrons sont 25 % plus faibles que le profil Expedition Infinity ; fenêtres de défense normales. Inclut la maîtrise immédiate des pictos.",
                ["Enemy HP bonus is doubled and speed/attack bonuses are 50% stronger than the Expedition Infinity preset; half Lumina."] =
                    "Le bonus de PV est doublé et les bonus de vitesse/attaque sont 50 % plus forts que le profil Expedition Infinity ; moitié moins de Lumina.",
                ["Adjust Lumina drops"] = "Ajuster les Lumina",
                ["× (0.5 = half, 2 = double)"] = "× (0,5 = moitié, 2 = double)",
                ["0.5 halves regular Lumina drops; 2 doubles them. Dialogue rewards are not changed."] =
                    "0,5 divise par deux les Lumina normaux ; 2 les double. Les récompenses de dialogue sont inchangées.",
                ["Max AP 21 · 3 consecutive turns · max Foretell 30 · max shields 5 · burn 0.075 / 0.01 / 3 turns · crit addend 1.5 with crit-rate scaling. These values are fixed in the generator."] =
                    "AP max 21 · 3 tours consécutifs · Présage max 30 · max. 5 boucliers · brûlure 0,075 / 0,01 / 3 tours · bonus critique 1,5 avec mise à l'échelle du taux critique. Ces valeurs sont fixes dans le générateur.",
                ["Regular Nevrons"] = "Nevrons normaux",
                ["Bosses"] = "Boss",
                ["Rolled stat bands and extra adjustments for regular Nevrons."] =
                    "Bandes de stats tirées et ajustements supplémentaires pour les Nevrons normaux.",
                ["Boss stat bands. Regular Nevron adjustments do not affect bosses."] =
                    "Bandes de stats des boss. Les ajustements des Nevrons normaux ne touchent pas les boss.",
                ["Reset regular bands"] = "Réinitialiser les bandes normales",
                ["Restore the default regular Nevron stat bands."] =
                    "Restaurer les bandes de stats par défaut des Nevrons normaux.",
                ["Reset boss bands"] = "Réinitialiser les bandes des boss",
                ["Restore the default boss stat bands."] =
                    "Restaurer les bandes de stats par défaut des boss.",
                ["Light stains"] = "Pigments de Lumière",
                ["Open the spoiler folder after a build"] =
                    "Ouvrir le dossier du spoiler après une création",
                ["Open the folder that contains the generated spoiler page."] =
                    "Ouvrir le dossier qui contient la page de spoiler générée.",
                ["Reset regular boosts"] = "Réinitialiser les boosts normaux",
                ["Restore the default regular Nevron boosts."] = "Restaurer les boosts par défaut des Nevrons normaux.",
                ["Percentage boosts applied after the seed roll. 0% = no boost."] =
                    "Boosts en pourcentage appliqués après le tirage. 0 % = aucun boost.",
                ["Apply regular Nevron percentage boosts"] = "Appliquer les boosts en pourcentage des Nevrons normaux",
                ["Positive percentages strengthen regular Nevrons; negative percentages weaken them. Bosses and alphas are not affected."] =
                    "Les pourcentages positifs renforcent les Nevrons normaux ; les pourcentages négatifs les affaiblissent. Les boss et alphas ne sont pas affectés.",
                ["HP boost T1–T5"] = "Boost de PV T1–T5",
                ["Percentage boost to regular Nevron HP. 0% = no boost."] =
                    "Boost en pourcentage des PV des Nevrons normaux. 0 % = aucun boost.",
                ["Speed boost T1–T5"] = "Boost de vitesse T1–T5",
                ["Percentage boost to regular Nevron speed. 0% = no boost."] =
                    "Boost en pourcentage de la vitesse des Nevrons normaux. 0 % = aucun boost.",
                ["Attack boost T1–T5"] = "Boost d'attaque T1–T5",
                ["Percentage boost to regular Nevron attack. 0% = no boost."] =
                    "Boost en pourcentage de l'attaque des Nevrons normaux. 0 % = aucun boost.",
                ["Reset boss boosts"] = "Réinitialiser les boosts des boss",
                ["Restore the default boss boosts."] = "Restaurer les boosts par défaut des boss.",
                ["Percentage boosts over vanilla boss values. Regular Nevron boosts do not affect bosses."] =
                    "Boosts en pourcentage par rapport aux valeurs de boss d'origine. Les boosts des Nevrons normaux ne touchent pas les boss.",
                ["Boss HP boost T1–T5"] = "Boost de PV des boss T1–T5",
                ["Percentage boost to boss HP for T1–T3. 0% = vanilla."] =
                    "Boost en pourcentage des PV des boss pour T1–T3. 0 % = origine.",
                ["curve"] = "courbe",
                ["T4–T5 boss HP is controlled by the hidden endgame curve."] =
                    "Les PV des boss T4–T5 sont contrôlés par la courbe de fin de partie masquée.",
                ["T4–T5 boss HP uses the hidden endgame curve and has no exposed control."] =
                    "Les PV des boss T4–T5 utilisent la courbe de fin de partie masquée et n'ont aucun contrôle exposé.",
                ["Boss speed boost"] = "Boost de vitesse des boss",
                ["Percentage boost to boss speed. 0% = vanilla."] =
                    "Boost en pourcentage de la vitesse des boss. 0 % = origine.",
                ["Extreme T4–T5 boss defense"] = "Défense extrême des boss T4–T5",
                ["Off uses the regular T4–T5 boss defense curve. On ramps defense from 1× at the T4 floor to 30× the regular curve at the top of T5."] =
                    "Désactivé utilise la courbe normale de défense des boss T4–T5. Activé fait passer la défense de 1× au début des T4 à 30× la courbe normale au sommet des T5.",
                ["Expedition Infinity is already running."] = "Expedition Infinity est déjà en cours d'exécution.",
                ["Close the other generator window before starting another build. Two instances would race over the donor cache and the installed seed."] =
                    "Fermez l'autre fenêtre du générateur avant de lancer une autre création. Deux instances se disputeraient le cache et la graine installée.",
            },
            ["pl"] = new(StringComparer.Ordinal)
            {
                ["Randomizer"] = "Losowanie",
                ["Balance"] = "Balans",
                ["Quality of Life"] = "Komfort gry",
                ["Language"] = "Język",
                ["Loot sources"] = "Źródła łupu",
                ["Build a seed pack for Expedition Infinity."] = "Zbuduj pakiet ziarna dla Expedition Infinity.",
                ["Core movesets, pictos and weapons are always included. The seed controls loot, stats and encounters."] =
                    "Podstawowe umiejętności, pictosy i bronie są zawsze uwzględnione. Ziarno steruje łupem, statystykami i walkami.",
                ["Setup"] = "Ustawienia",
                ["Game folder, output folder and profile."] = "Folder gry, folder instalacji i profil.",
                ["Game folder"] = "Folder gry",
                ["The folder that contains the \"Sandfall\" folder."] = "Folder zawierający katalog „Sandfall”.",
                ["Usually found automatically. Browse only if needed."] = "Zwykle znajdowany automatycznie. Przeglądaj tylko w razie potrzeby.",
                ["Choose the game folder manually."] = "Wybierz folder gry ręcznie.",
                ["Find automatically"] = "Znajdź automatycznie",
                ["Search the usual Steam folders."] = "Przeszukaj typowe foldery Steam.",
                ["Install folder"] = "Folder instalacji",
                ["Where the finished seed pack is installed. The game reads it from its ~mods folder."] =
                    "Miejsce instalacji pakietu. Gra odczytuje go z folderu ~mods.",
                ["Defaults to the game's ~mods folder. Created if missing."] =
                    "Domyślnie folder ~mods gry. Tworzony, jeśli go nie ma.",
                ["Choose a different install folder."] = "Wybierz inny folder instalacji.",
                ["Use default"] = "Użyj domyślnego",
                ["Use the game's standard mod folder (~mods)."] = "Użyj standardowego folderu modów gry (~mods).",
                ["Profile"] = "Profil",
                ["Save or load your seed settings."] = "Zapisz lub wczytaj ustawienia ziarna.",
                ["Save profile…"] = "Zapisz profil…",
                ["Save these settings so you can reuse them."] = "Zapisz te ustawienia do ponownego użycia.",
                ["Load profile…"] = "Wczytaj profil…",
                ["Load a saved settings file."] = "Wczytaj zapisany plik ustawień.",
                ["Browse…"] = "Przeglądaj…",
                ["Seed"] = "Ziarno",
                ["Same seed and profile give the same results."] = "To samo ziarno i profil dają te same wyniki.",
                ["Copy this seed to recreate the same pack later."] = "Skopiuj ziarno, aby później odtworzyć ten sam pakiet.",
                ["Random"] = "Losuj",
                ["Pick a new random seed."] = "Wylosuj nowe ziarno.",
                ["Copy"] = "Kopiuj",
                ["Copy the seed."] = "Kopiuj ziarno.",
                ["Build seed pack"] = "Zbuduj pakiet ziarna",
                ["Build the pack and install it into the game's ~mods folder."] =
                    "Zbuduj pakiet i zainstaluj go w folderze ~mods gry.",
                ["Cancel"] = "Anuluj",
                ["Cancel between build stages; the installed pack is not replaced."] =
                    "Anuluj między etapami; zainstalowany pakiet nie zostanie zastąpiony.",
                ["Open install folder"] = "Otwórz folder instalacji",
                ["Open the game's ~mods folder."] = "Otwórz folder ~mods gry.",
                ["Open app folder"] = "Otwórz folder aplikacji",
                ["Open the application folder that stores seeds and caches."] =
                    "Otwórz folder aplikacji z ziarnami i pamięcią podręczną.",
                ["Open seed library"] = "Otwórz bibliotekę ziaren",
                ["Open the folder that keeps every generated seed."] =
                    "Otwórz folder przechowujący wszystkie wygenerowane ziarna.",
                ["Copy log"] = "Kopiuj dziennik",
                ["Copy the build log for troubleshooting."] = "Kopiuj dziennik budowania do diagnozy.",
                ["Clear log"] = "Wyczyść dziennik",
                ["Clear the log window."] = "Wyczyść okno dziennika.",
                ["Ready."] = "Gotowe.",
                ["Idle."] = "Bezczynne.",
                ["Game folder not found."] = "Nie znaleziono folderu gry.",
                ["Game folder ready."] = "Folder gry gotowy.",
                ["That folder does not look like Expedition 33."] = "Ten folder nie wygląda na Expedition 33.",
                ["Expedition 33 found. Confirm the folder."] = "Znaleziono Expedition 33. Potwierdź folder.",
                ["The application folder is not writable."] = "Folder aplikacji nie pozwala na zapis.",
                ["Build cancelled before extraction."] = "Budowanie anulowane przed rozpakowaniem.",
                ["Build cancelled. The previously installed pack was not replaced."] =
                    "Budowanie anulowane. Poprzedni pakiet nie został zastąpiony.",
                ["Pack failed."] = "Budowanie pakietu nie powiodło się.",
                ["Building. Quit the game completely before packing."] =
                    "Budowanie. Zamknij całkowicie grę przed pakowaniem.",
                ["Building…"] = "Budowanie…",
                ["Extracting…"] = "Rozpakowywanie…",
                ["Cancelling after the current stage…"] = "Anulowanie po bieżącym etapie…",
                ["Cancelling build; please wait."] = "Anulowanie budowania; poczekaj.",
                ["Preparing game data"] = "Przygotowywanie danych gry",
                ["Extracting game data. This can take several minutes; keep the app open."] =
                    "Rozpakowywanie danych gry. Może to potrwać kilka minut; pozostaw aplikację otwartą.",
                ["Game data ready"] = "Dane gry gotowe",
                ["Game data extraction failed."] = "Rozpakowywanie danych gry nie powiodło się.",
                ["Game data extraction skipped. The first build will ask again."] =
                    "Pominięto rozpakowywanie. Pierwsze budowanie zapyta ponownie.",
                ["Language changed."] = "Zmieniono język.",
                ["Type a seed or roll a new one."] = "Wpisz ziarno lub wylosuj nowe.",
                ["Type a number from 0 to 99,999,999."] = "Wpisz liczbę od 0 do 99 999 999.",
                ["Pick a random seed."] = "Wylosuj ziarno.",
                ["Name of the pack that will be generated."] = "Nazwa pakietu, który zostanie utworzony.",
                ["Your last 20 builds."] = "Twoje ostatnie 20 pakietów.",
                ["Seed history"] = "Historia ziaren",
                ["Recent builds, newest first."] = "Ostatnie pakiety, od najnowszego.",
                ["Restore seed + profile"] = "Przywróć ziarno + profil",
                ["Reload the seed and all settings from the selected build."] =
                    "Wczytaj ziarno i wszystkie ustawienia z wybranego pakietu.",
                ["The seed controls loot, stats and encounters. Core content is always included."] =
                    "Ziarno steruje łupem, statystykami i walkami. Podstawowa zawartość jest zawsze uwzględniona.",
                ["Choose what the seed can change. Keys, journals, music and quest items stay vanilla."] =
                    "Wybierz, co może zmieniać ziarno. Klucze, dzienniki, muzyka i przedmioty zadań pozostają bez zmian.",
                ["Chests and pickups"] = "Skrzynie i przedmioty",
                ["Pictos and weapons found in chests and pickups."] = "Pictosy i bronie ze skrzyń i przedmiotów.",
                ["Paint Cages"] = "Klatki malarskie",
                ["Each cage rolls one tier-appropriate picto or weapon."] =
                    "Każda klatka daje pictos lub broń odpowiednią dla poziomu.",
                ["Enemy drops"] = "Łup z wrogów",
                ["Regular, elite, boss and overworld drops."] = "Łup z wrogów zwykłych, elit, bossów i świata.",
                ["Merchant inventories"] = "Asortyment kupców",
                ["Tier-appropriate shop stock."] = "Towar odpowiedni dla poziomu.",
                ["Weapon passives"] = "Pasywy broni",
                ["Rolls the passive effect on weapons."] = "Losuje efekt pasywny broni.",
                ["Monster stats"] = "Statystyki potworów",
                ["Rolls enemy HP, attack, speed and defense."] = "Losuje HP, atak, szybkość i obronę wrogów.",
                ["Weapon stats and elements"] = "Statystyki i żywioły broni",
                ["Rolls weapon attack, free-aim and element."] = "Losuje atak, celowanie swobodne i żywioł broni.",
                ["Battle groups"] = "Grupy walk",
                ["Adds extra enemies to regular encounters. Experimental."] =
                    "Dodaje wrogów do zwykłych walk. Funkcja eksperymentalna.",
                ["Re-roll encounters"] = "Losuj walki ponownie",
                ["Picks a fresh encounter each time a battle starts. Experimental."] =
                    "Wybierze nową walkę przy każdym rozpoczęciu. Funkcja eksperymentalna.",
                ["All off: the seed changes nothing; core content and Balance/QoL still apply."] =
                    "Wszystko wyłączone: ziarno nic nie zmienia; zawartość podstawowa i ustawienia nadal działają.",
                ["Loot tuning"] = "Dostrajanie łupu",
                ["Drop rates, shop stock and experimental battle groups."] =
                    "Szanse łupu, asortyment sklepów i eksperymentalne grupy walk.",
                ["Drop rates and shop stock."] = "Szanse łupu i asortyment sklepów.",
                ["Regular enemy pictos chance"] = "Szansa pictosa ze zwykłego wroga",
                ["Chance a regular enemy drops a picto."] = "Szansa, że zwykły wróg upuści pictos.",
                ["Elite enemy pictos chance"] = "Szansa pictosa z elitarnego wroga",
                ["Chance an elite enemy drops a picto."] = "Szansa, że elitarny wróg upuści pictos.",
                ["Perfect catalyst chance"] = "Szansa doskonałego katalizatora",
                ["Rare endgame catalyst. 3.125% is about 1 in 32. Set 0 to disable."] =
                    "Rzadki katalizator końcówki gry. 3,125% to około 1 na 32. Ustaw 0, aby wyłączyć.",
                ["Petank Colour of Lumina"] = "Kolor Lumina Petanka",
                ["Lumina awarded by a Petank."] = "Lumina przyznawana przez Petanka.",
                ["Merchant pictos + weapon slots"] = "Slot pictosów + broni u kupca",
                ["Extra gear slots in shops."] = "Dodatkowe sloty ekwipunku w sklepach.",
                ["Tint shards per type (T1–T4)"] = "Odłamki barwnika na typ (T1–T4)",
                ["Tint shards found in early and mid-game chests."] = "Odłamki barwnika ze skrzyń wczesnej i środkowej gry.",
                ["Chroma Elixir shards (T1–T4)"] = "Odłamki eliksiru chromy (T1–T4)",
                ["Chroma Elixir shards found in early and mid-game chests."] =
                    "Odłamki eliksiru chromy ze skrzyń wczesnej i środkowej gry.",
                ["Shapes per type (T1–T4)"] = "Kształty na typ (T1–T4)",
                ["Shapes found in early and mid-game chests."] = "Kształty ze skrzyń wczesnej i środkowej gry.",
                ["Max enemies per battle"] = "Maks. wrogów w walce",
                ["Upper limit for battle groups. Bosses and scripted fights are excluded."] =
                    "Limit grup walk. Bossowie i walki fabularne są wykluczone.",
                ["Battle groups area"] = "Obszar grup walk",
                ["Flying Waters (test area)"] = "Latające Wody (obszar testowy)",
                ["All eligible areas"] = "Wszystkie odpowiednie obszary",
                ["Limit battle groups to the test area or allow every eligible encounter."] =
                    "Ogranicz grupy do obszaru testowego lub zezwól na wszystkie odpowiednie walki.",
                ["Chance to add enemies"] = "Szansa dodania wrogów",
                ["How often a regular encounter gets extra enemies."] = "Jak często zwykła walka dostaje dodatkowych wrogów.",
                ["Shuffle final battle rosters"] = "Losuj końcowe składy walk",
                ["Randomize the order of enemies in the final group."] = "Losuje kolejność wrogów w końcowej grupie.",
                ["Variants per encounter"] = "Warianty na walkę",
                ["How many alternate groups are prepared for each encounter."] =
                    "Ile alternatywnych grup jest przygotowanych dla każdej walki.",
                ["Enemy stat bands"] = "Zakresy statystyk wrogów",
                ["Multipliers per tier. 1.0 is vanilla."] = "Mnożniki na poziom. 1,0 to wartość bazowa.",
                ["Enemy stat min"] = "Min. statystyk wroga",
                ["Enemy stat max"] = "Maks. statystyk wroga",
                ["Lowest non-boss stat multiplier for each tier."] = "Najniższy mnożnik statystyk (poza bossami) na poziom.",
                ["Highest non-boss stat multiplier for each tier."] = "Najwyższy mnożnik statystyk (poza bossami) na poziom.",
                ["Boss HP ×"] = "HP bossa ×",
                ["Boss HP multiplier for each tier."] = "Mnożnik HP bossa na poziom.",
                ["Attack min"] = "Min. ataku",
                ["Attack max"] = "Maks. ataku",
                ["Lowest physical and magical attack multiplier."] = "Najniższy mnożnik ataku fizycznego i magicznego.",
                ["Highest physical and magical attack multiplier."] = "Najwyższy mnożnik ataku fizycznego i magicznego.",
                ["Boss speed ×"] = "Szybkość bossa ×",
                ["Extra speed multiplier applied to bosses."] = "Dodatkowy mnożnik szybkości bossów.",
                ["Reset to Expedition preset"] = "Przywróć profil Expedition",
                ["Restore the default stat bands."] = "Przywróć domyślne zakresy statystyk.",
                ["Adjust difficulty and growth after the seed rolls."] = "Dostosuj trudność i rozwój po wylosowaniu ziarna.",
                ["Enemy and lumina balance"] = "Balans wrogów i Luminy",
                ["Adjust regular enemy stats by tier"] = "Dostosuj statystyki zwykłych wrogów na poziom",
                ["Positive values strengthen regular enemies; negative values weaken them. Bosses and alphas are not affected."] =
                    "Wartości dodatnie wzmacniają zwykłych wrogów; ujemne ich osłabiają. Bossowie i alfy nie są zmieniani.",
                ["HP adjustment T1–T5"] = "Korekta HP T1–T5",
                ["Added to normal enemy HP. 0% = vanilla; negative values weaken."] =
                    "Dodawane do HP zwykłych wrogów. 0% = bazowo; wartości ujemne osłabiają.",
                ["Speed adjustment T1–T5"] = "Korekta szybkości T1–T5",
                ["Added to normal enemy speed. 0% = vanilla; negative values weaken."] =
                    "Dodawane do szybkości zwykłych wrogów. 0% = bazowo; wartości ujemne osłabiają.",
                ["Attack adjustment T1–T5"] = "Korekta ataku T1–T5",
                ["Added to normal enemy attack. 0% = vanilla; negative values weaken."] =
                    "Dodawane do ataku zwykłych wrogów. 0% = bazowo; wartości ujemne osłabiają.",
                ["Reduce regular lumina drops"] = "Zmniejsz Luminy ze zwykłych wrogów",
                ["Chests, enemy drops and shops only. Dialogue rewards are untouched."] =
                    "Tylko skrzynie, łup i sklepy. Nagrody z dialogów pozostają bez zmian.",
                ["Lumina multiplier"] = "Mnożnik Luminy",
                ["0.5 halves regular lumina drops. Dialogue rewards are not changed."] =
                    "0,5 zmniejsza o połowę zwykłe Luminy. Nagrody z dialogów pozostają bez zmian.",
                ["Progression"] = "Rozwój",
                ["Level and stat growth."] = "Poziomy i rozwój statystyk.",
                ["Character level cap"] = "Maks. poziom postaci",
                ["Maximum character level."] = "Maksymalny poziom postaci.",
                ["Attribute point cap"] = "Limit punktów atrybutów",
                ["Maximum points in a single attribute."] = "Maksymalna liczba punktów w jednym atrybucie.",
                ["XP per extra level (100+)"] = "XP za dodatkowy poziom (100+)",
                ["XP needed for each level above 99."] = "XP potrzebne na każdy poziom powyżej 99.",
                ["Attribute points per level"] = "Punkty atrybutów na poziom",
                ["Attribute points gained per level."] = "Punkty atrybutów zdobywane na poziom.",
                ["Skill-tree points per level"] = "Punkty drzewka umiejętności na poziom",
                ["Skill points gained per level."] = "Punkty umiejętności zdobywane na poziom.",
                ["Lumina points per level"] = "Punkty Luminy na poziom",
                ["Lumina gained per level."] = "Lumina zdobywana na poziom.",
                ["Player power"] = "Siła gracza",
                ["Multipliers on the normal level curves. 1.0 is intended."] =
                    "Mnożniki normalnych krzywych poziomów. 1,0 to wartość docelowa.",
                ["Max HP ×"] = "Maks. HP ×",
                ["Physical Attack ×"] = "Atak fizyczny ×",
                ["Speed ×"] = "Szybkość ×",
                ["Critical Rate ×"] = "Szansa krytyczna ×",
                ["Defense ×"] = "Obrona ×",
                ["Difficulty"] = "Trudność",
                ["Dodge and parry timing."] = "Czas uniku i parowania.",
                ["Use tuned windows"] = "Użyj dostrojonych okien",
                ["Shorter windows make dodging and parrying harder."] = "Krótsze okna utrudniają unik i parowanie.",
                ["Expedition (0.165 / 0.1125)"] = "Expedition (0,165 / 0,1125)",
                ["Tighter dodge and parry windows."] = "Krótsze okna uniku i parowania.",
                ["Normal (0.22 / 0.15)"] = "Normalne (0,22 / 0,15)",
                ["Vanilla dodge and parry windows."] = "Bazowe okna uniku i parowania.",
                ["Dodge window (seconds)"] = "Okno uniku (sekundy)",
                ["How long a dodge input is accepted."] = "Jak długo przyjmowany jest unik.",
                ["Parry window (seconds)"] = "Okno parowania (sekundy)",
                ["How long a parry input is accepted."] = "Jak długo przyjmowane jest parowanie.",
                ["Weapon progression"] = "Rozwój broni",
                ["Scale weapon drop levels"] = "Skaluj poziomy wypadających broni",
                ["Extends weapon drops beyond the vanilla level range."] = "Rozszerza wypadanie broni poza bazowy zakres.",
                ["Max weapon drop level"] = "Maks. poziom wypadającej broni",
                ["Weapon drops scale from 1 to this level. Pictos stay 1–33."] =
                    "Broń wypada od 1 do tego poziomu. Pictosy pozostają 1–33.",
                ["Fixed core tuning"] = "Stałe ustawienia podstawowe",
                ["Combat basics stay at the intended Expedition Infinity values."] =
                    "Podstawy walki pozostają na wartościach Expedition Infinity.",
                ["AP, turns, burn and critical damage are fixed."] = "AP, tury, podpalenie i obrażenia krytyczne są stałe.",
                ["Convenience changes. Core balance is unchanged."] = "Udogodnienia. Podstawowy balans się nie zmienia.",
                ["Picto mastery"] = "Mistrzostwo pictosów",
                ["Instant Picto mastery"] = "Natychmiastowe mistrzostwo pictosów",
                ["Instant mastery on pickup (always on)"] = "Natychmiastowe mistrzostwo po zdobyciu (zawsze włączone)",
                ["A found picto is mastered immediately. It does not grant pictos you have not found."] =
                    "Znaleziony pictos jest od razu opanowany. Nie daje pictosów, których nie znaleziono.",
                ["Battle proc cards"] = "Karty proc w walce",
                ["Max battle proc cards (0 = vanilla)"] = "Maks. kart efektów w walce (0 = bazowo)",
                ["Limits how many picto/passive cards can stack on screen during battle. 0 leaves it vanilla."] =
                    "Ogranicza liczbę kart pictosów/pasywów na ekranie. 0 pozostawia wartość bazową.",
                ["Tutorials and stains"] = "Samouczki i barwniki",
                ["Skip tutorials"] = "Pomiń samouczki",
                ["Tutorial fights are replaced by their normal enemy versions."] =
                    "Walki samouczka są zastępowane normalnymi wersjami wrogów.",
                ["Restore Light-stain substitution for Lune"] = "Przywróć zamianę barwnika Światła dla Lune",
                ["A held Light stain can cover another missing element. Elemental Genesis, Cataclysm and Mayhem stay strict."] =
                    "Trzymany barwnik Światła może zastąpić inny brakujący żywioł. Geneza, Kataklizm i Mayhem pozostają ścisłe.",
                ["Weapon upgrades"] = "Ulepszenia broni",
                ["Bulk upgrade"] = "Ulepszanie grupowE",
                ["Hold to upgrade multiple levels at once, up to the current catalyst tier limit."] =
                    "Przytrzymaj, aby ulepszyć kilka poziomów naraz, do obecnego limitu poziomu katalizatora.",
                ["Levels per hold"] = "Poziomy na przytrzymanie",
                ["Maximum levels added by one hold."] = "Maksymalna liczba poziomów dodana jednym przytrzymaniem.",
                ["Boss movesets"] = "Zestawy ruchów bossów",
                ["Mask Keeper keeps the full moveset"] = "Strażnik Masek zachowuje pełny zestaw ruchów",
                ["The Mask Keeper keeps its mask moves after the sub-bosses are defeated."] =
                    "Strażnik Masek zachowuje ruchy masek po pokonaniu podbossów.",
                ["Sirène keeps her full moveset"] = "Sirène zachowuje pełny zestaw ruchów",
                ["Sirène keeps her Tisseur-synergy moves after Tisseur is defeated."] =
                    "Sirène zachowuje ruchy synergii z Tisseurem po jego pokonaniu.",
                ["Five-member party: always on."] = "Drużyna pięciu osób: zawsze włączona.",
                ["Build convenience"] = "Udogodnienia budowania",
                ["Open the install folder after a build"] = "Otwórz folder instalacji po zbudowaniu",
                ["Open the game's ~mods folder when the build finishes."] = "Otwórz folder ~mods gry po zakończeniu.",
                ["Open the spoiler after a build"] = "Otwórz spoiler po zbudowaniu",
                ["Open the offline spoiler page in your browser."] = "Otwórz offline stronę spoilerów w przeglądarce.",
                ["Language of the pack and the app."] = "Język pakietu i aplikacji.",
                ["Changes the pack and app language."] = "Zmienia język pakietu i aplikacji.",
                ["Choose the folder that contains the \"Sandfall\" folder."] = "Wybierz folder zawierający katalog „Sandfall”.",
                ["We couldn't find Expedition 33 in the usual Steam folders."] =
                    "Nie znaleziono Expedition 33 w typowych folderach Steam.",
                ["Typical location: Steam → steamapps → common → Expedition 33"] =
                    "Typowa lokalizacja: Steam → steamapps → common → Expedition 33",
                ["Quit"] = "Zakończ",
                ["Choose folder…"] = "Wybierz folder…",
                ["Find Expedition 33"] = "Znajdź Expedition 33",
                ["Expedition 33 found"] = "Znaleziono Expedition 33",
                ["Use this folder"] = "Użyj tego folderu",
                ["Choose another folder…"] = "Wybierz inny folder…",
                ["By default, packs install to the game's ~mods folder. You can change both folders later in Setup."] =
                    "Domyślnie pakiety instalują się w folderze ~mods gry. Oba foldery możesz zmienić później w Ustawieniach.",
                ["First-time setup"] = "Pierwsza konfiguracja",
                ["Finish game data setup"] = "Dokończ konfigurację danych gry",
                ["Expedition Infinity needs some data from your game before it can build seeds. This is a one-time step."] =
                    "Expedition Infinity potrzebuje danych z gry, aby tworzyć ziarna. To jednorazowy krok.",
                ["Most game data is already prepared. {0} of {1} item(s) still need to be read from your game."] =
                    "Większość danych gry jest gotowa. Pozostało {0} z {1} elementów do odczytania.",
                ["• About 1.2 GB of files"] = "• Około 1,2 GB plików",
                ["• Usually 5–15 minutes, depending on the drive"] = "• Zwykle 5–15 minut, zależnie od dysku",
                ["• Stored inside this application's folder"] = "• Zapisywane w folderze tej aplikacji",
                ["• Quit Expedition 33 before continuing"] = "• Zamknij Expedition 33 przed kontynuowaniem",
                ["• No game files are modified"] = "• Pliki gry nie są modyfikowane",
                ["• Only the missing items are read"] = "• Odczytywane są tylko brakujące elementy",
                ["• Usually quick"] = "• Zwykle szybko",
                ["Saved to"] = "Zapisywane w",
                ["If you cancel, the generator will ask again before the first build or the next launch. You can still choose a seed, profile and language."] =
                    "Po anulowaniu generator zapyta ponownie przed pierwszym budowaniem lub następnym uruchomieniem. Nadal możesz wybrać ziarno, profil i język.",
                ["Start extraction"] = "Rozpocznij rozpakowywanie",
                ["Finish setup"] = "Dokończ konfigurację",
                ["Not now"] = "Nie teraz",
                ["Existing game data is reused; nothing is downloaded again."] =
                    "Istniejące dane gry są używane ponownie; nic nie jest pobierane od nowa.",
                ["This is a one-time setup step."] = "To jednorazowy krok konfiguracji.",
                ["Preparing game data…"] = "Przygotowywanie danych gry…",
                ["Continue"] = "Kontynuuj",
                ["Discard custom changes?"] = "Odrzucić zmiany niestandardowe?",
                ["Loading {0} will replace your custom settings."] = "Wczytanie {0} zastąpi Twoje ustawienia niestandardowe.",
                ["Custom"] = "Niestandardowy",
                ["Gentler"] = "Łagodniejszy",
                ["Brutal"] = "Brutalny",
                ["Loot only"] = "Tylko łup",
                ["Stats only"] = "Tylko statystyki",
                ["Restored seed {0}."] = "Przywrócono ziarno {0}.",
                ["Loading…"] = "Ładowanie…",
                ["Could not open the generator."] = "Nie można otworzyć generatora.",
                ["Pack: {0}"] = "Pakiet: {0}",
                ["× (0.5 = half)"] = "× (0,5 = połowa)",
                ["Regular enemy stats are 25% lower than the Expedition Infinity preset; normal defense windows."] =
                    "Statystyki zwykłych wrogów są o 25% niższe niż w profilu Expedition Infinity; normalne okna obrony.",
                ["Enemy HP is doubled and speed/attack are 50% higher than the Expedition Infinity preset; half lumina."] =
                    "HP wrogów jest podwojone, a szybkość i atak są o 50% wyższe niż w profilu Expedition Infinity; połowa Luminy.",
                ["Standard combat; only loot rolls change."] =
                    "Standardowa walka; zmieniają się tylko losy łupu.",
                ["Standard combat; only enemy and weapon stats change."] =
                    "Standardowa walka; zmieniają się tylko statystyki wrogów i broni.",
                ["Your own custom settings."] = "Twoje własne ustawienia.",
                ["Standard Expedition Infinity combat and progression."] =
                    "Standardowa walka i rozwój Expedition Infinity.",
                ["Checking settings"] = "Sprawdzanie ustawień",
                ["Preparing tools"] = "Przygotowywanie narzędzi",
                ["Building core content"] = "Budowanie podstawowej zawartości",
                ["Applying balance"] = "Stosowanie balansu",
                ["Applying quality of life"] = "Stosowanie udogodnień",
                ["Rolling the seed"] = "Losowanie ziarna",
                ["Applying language"] = "Stosowanie języka",
                ["Writing the spoiler"] = "Zapisywanie spoilerów",
                ["Packing the seed"] = "Pakowanie ziarna",
                ["Checking the pack"] = "Sprawdzanie pakietu",
                ["Installing"] = "Instalowanie",
                ["Done"] = "Gotowe",
                ["enemy data"] = "dane wrogów",
                ["weapon and gear data"] = "dane broni i ekwipunku",
                ["skill data"] = "dane umiejętności",
                ["picto data"] = "dane pictosów",
                ["loot and merchant data"] = "dane łupu i kupców",
                ["text and localization"] = "teksty i lokalizacja",
                ["battle data"] = "dane walk",
                ["character data"] = "dane postaci",
                ["game blueprints"] = "plany gry",
                ["game data assets"] = "zasoby gry",
                ["game assets"] = "zasoby gry",
                ["game data"] = "dane gry",
                ["Extracting {0}…"] = "Rozpakowywanie: {0}…",
                ["{0} of {1} tasks complete ({2}%)"] = "{0} z {1} zadań ukończonych ({2}%)",
                ["This is a one-time setup. Keep this window open."] = "To jednorazowa konfiguracja. Pozostaw to okno otwarte.",
                ["Checking which game files are needed…"] = "Sprawdzanie potrzebnych plików gry…",
                ["Expedition 33 game folder"] = "Folder gry Expedition 33",
                ["Game data ready."] = "Dane gry gotowe.",
                ["Game data ready — {0} extraction task(s) completed."] = "Dane gry gotowe — ukończono zadań: {0}.",
                ["Game files not found in that folder."] = "Nie znaleziono plików gry w tym folderze.",
                ["Install folder (created if missing)"] = "Folder instalacji (tworzony, jeśli go nie ma)",
                ["Install folder is not writable."] = "Folder instalacji nie pozwala na zapis.",
                ["Max AP 21 · 3 consecutive turns · max Foretell 30 · max shields 5 · burn 0.075 / 0.01 / 3 turns · crit addend 1.5 with crit-rate scaling. Use the full editor for experimental changes."] =
                    "Maks. AP 21 · 3 tury pod rząd · maks. Przepowiednia 30 · maks. tarcze 5 · podpalenie 0,075 / 0,01 / 3 tury · bonus kryta 1,5 ze skalowaniem od szansy kryta. Do eksperymentów użyj pełnego edytora.",
                ["New seed"] = "Nowe ziarno",
                ["Profile loaded."] = "Profil wczytany.",
                ["Profile saved."] = "Profil zapisany.",
                ["Settings validation failed."] = "Walidacja ustawień nie powiodła się.",
                ["Starting…"] = "Uruchamianie…",
                ["This one-time setup reads game data. Later builds skip it."] = "Ta jednorazowa konfiguracja odczytuje dane gry. Kolejne budowania ją pomijają.",
                ["vanilla 0.15"] = "bazowo 0,15",
                ["vanilla 0.22"] = "bazowo 0,22",
                ["Packed {0} ({1} edits) into {2}"] = "Utworzono pakiet {0} ({1} zmian) w {2}",
                ["Packed {0} ({1} edits); library {2}; installed in {3}"] = "Utworzono pakiet {0} ({1} zmian); biblioteka {2}; zainstalowano w {3}",
                ["That folder does not look like Expedition 33. Choose the folder that contains the \"Sandfall\" folder."] =
                    "Ten folder nie wygląda na Expedition 33. Wybierz folder zawierający katalog „Sandfall”.",
                ["Choose the Expedition 33 game folder"] = "Wybierz folder gry Expedition 33",
                ["Choose the install folder"] = "Wybierz folder instalacji",
                ["Save Expedition Infinity profile"] = "Zapisz profil Expedition Infinity",
                ["Load Expedition Infinity profile"] = "Wczytaj profil Expedition Infinity",
                ["Expedition Infinity profile"] = "Profil Expedition Infinity",
                ["The seed controls loot, Nevron stats and encounters. Core content is always included."] =
                    "Ziarno steruje łupem, statystykami Nevronów i walkami. Podstawowa zawartość jest zawsze uwzględniona.",
                ["Loot"] = "Łup",
                ["Loot options"] = "Opcje łupu",
                ["Nevrons"] = "Nevrony",
                ["Nevron options"] = "Opcje Nevronów",
                ["Choose which loot sources the seed changes. Keys, journals, music and quest items stay vanilla."] =
                    "Wybierz źródła łupu zmieniane przez ziarno. Klucze, dzienniki, muzyka i przedmioty zadań pozostają bez zmian.",
                ["Nevron drops"] = "Łup z Nevronów",
                ["Regular, elite, boss and overworld Nevron drops."] =
                    "Łup ze zwykłych Nevronów, elit, bossów i świata.",
                ["Drop rates, weapon rolls and loot quantities."] = "Szanse łupu, losy broni i ilości łupu.",
                ["Regular Nevron picto chance"] = "Szansa pictosa ze zwykłego Nevrona",
                ["Chance a regular Nevron drops a picto."] = "Szansa, że zwykły Nevron upuści pictos.",
                ["Elite Nevron picto chance"] = "Szansa pictosa z elitarnego Nevrona",
                ["Chance an elite Nevron drops a picto."] = "Szansa, że elitarny Nevron upuści pictos.",
                ["Chests, Nevron drops and shops only. Dialogue rewards are untouched."] =
                    "Tylko skrzynie, łup z Nevronów i sklepy. Nagrody z dialogów pozostają bez zmian.",
                ["Choose whether Nevron stats are rerolled by the seed."] =
                    "Wybierz, czy statystyki Nevronów są losowane przez ziarno.",
                ["Nevron stats"] = "Statystyki Nevronów",
                ["Rolls Nevron HP, attack, speed and defense."] =
                    "Losuje HP, atak, szybkość i obronę Nevronów.",
                ["Turned off: the seed leaves Nevron stats at their rolled band defaults."] =
                    "Wyłączone: ziarno pozostawia statystyki Nevronów na domyślnych wartościach zakresu.",
                ["Rolled stat bands, followed by an optional extra adjustment."] =
                    "Losowane zakresy statystyk, a następnie opcjonalna dodatkowa korekta.",
                ["Rolled bands"] = "Losowane zakresy",
                ["Extra adjustment"] = "Dodatkowa korekta",
                ["Nevron stat min"] = "Min. statystyka Nevrona",
                ["Nevron stat max"] = "Maks. statystyka Nevrona",
                ["Reset bands to Expedition preset"] = "Przywróć zakresy profilu Expedition",
                ["Restore the default Nevron stat bands."] = "Przywróć domyślne zakresy statystyk Nevronów.",
                ["Adjust regular Nevron stats by tier"] = "Dostosuj statystyki zwykłych Nevronów na poziom",
                ["Positive values strengthen regular Nevrons; negative values weaken them. Bosses and alphas are not affected."] =
                    "Wartości dodatnie wzmacniają zwykłe Nevrony; ujemne je osłabiają. Bossowie i alfy nie są zmieniani.",
                ["Added after the roll. 0% = no extra adjustment."] =
                    "Dodawane po losowaniu. 0% = brak dodatkowej korekty.",
                ["All sources off: the seed changes nothing; core content and Balance/QoL still apply."] =
                    "Wszystkie źródła wyłączone: ziarno nic nie zmienia; zawartość podstawowa i ustawienia nadal działają.",
                ["Adjust progression, attributes, boss movesets and parry timing after the seed rolls."] =
                    "Dostosuj rozwój, atrybuty, zestawy ruchów bossów i czas parowania po wylosowaniu ziarna.",
                ["Attributes"] = "Atrybuty",
                ["Multipliers on the character's attribute curves. 1.0 is intended."] =
                    "Mnożniki krzywych atrybutów postaci. 1,0 to wartość docelowa.",
                ["Parry timings"] = "Czasy parowania",
                ["Dodge and parry windows."] = "Okna uniku i parowania.",
                ["These values are fixed in the generator."] = "Te wartości są stałe w generatorze.",
                ["Easy"] = "Łatwy",
                ["Enemy stat bonuses are 25% weaker than the Expedition Infinity preset; normal defense windows. Includes instant Picto mastery."] =
                    "Bonusy statystyk Nevronów są o 25% słabsze niż w profilu Expedition Infinity; normalne okna obrony. Obejmuje natychmiastowe mistrzostwo pictosów.",
                ["Enemy HP bonus is doubled and speed/attack bonuses are 50% stronger than the Expedition Infinity preset; half Lumina."] =
                    "Bonus HP jest podwojony, a bonusy szybkości/ataku są o 50% silniejsze niż w profilu Expedition Infinity; połowa Luminy.",
                ["Adjust Lumina drops"] = "Dostosuj Luminy",
                ["× (0.5 = half, 2 = double)"] = "× (0,5 = połowa, 2 = podwójnie)",
                ["0.5 halves regular Lumina drops; 2 doubles them. Dialogue rewards are not changed."] =
                    "0,5 zmniejsza o połowę zwykłe Luminy; 2 podwaja je. Nagrody z dialogów pozostają bez zmian.",
                ["Max AP 21 · 3 consecutive turns · max Foretell 30 · max shields 5 · burn 0.075 / 0.01 / 3 turns · crit addend 1.5 with crit-rate scaling. These values are fixed in the generator."] =
                    "Maks. AP 21 · 3 tury pod rząd · maks. Przepowiednia 30 · maks. 5 osłon · płonięcie 0,075 / 0,01 / 3 tury · bonus kryta 1,5 ze skalowaniem od szansy kryta. Te wartości są stałe w generatorze.",
                ["Regular Nevrons"] = "Zwykłe Nevrony",
                ["Bosses"] = "Bossowie",
                ["Rolled stat bands and extra adjustments for regular Nevrons."] =
                    "Losowane zakresy i dodatkowe korekty dla zwykłych Nevronów.",
                ["Boss stat bands. Regular Nevron adjustments do not affect bosses."] =
                    "Zakresy statystyk bossów. Korekty zwykłych Nevronów nie dotyczą bossów.",
                ["Reset regular bands"] = "Przywróć zwykłe zakresy",
                ["Restore the default regular Nevron stat bands."] =
                    "Przywróć domyślne zakresy statystyk zwykłych Nevronów.",
                ["Reset boss bands"] = "Przywróć zakresy bossów",
                ["Restore the default boss stat bands."] =
                    "Przywróć domyślne zakresy statystyk bossów.",
                ["Light stains"] = "Plamy Światła",
                ["Open the spoiler folder after a build"] =
                    "Otwórz folder spoilerów po zbudowaniu",
                ["Open the folder that contains the generated spoiler page."] =
                    "Otwórz folder zawierający wygenerowaną stronę spoilerów.",
                ["Reset regular boosts"] = "Przywróć zwykłe bonusy",
                ["Restore the default regular Nevron boosts."] = "Przywróć domyślne bonusy zwykłych Nevronów.",
                ["Percentage boosts applied after the seed roll. 0% = no boost."] =
                    "Bonusy procentowe stosowane po losowaniu. 0% = brak bonusu.",
                ["Apply regular Nevron percentage boosts"] = "Stosuj procentowe bonusy zwykłych Nevronów",
                ["Positive percentages strengthen regular Nevrons; negative percentages weaken them. Bosses and alphas are not affected."] =
                    "Dodatnie wartości wzmacniają zwykłe Nevrony; ujemne je osłabiają. Bossowie i alfy nie są zmieniani.",
                ["HP boost T1–T5"] = "Bonus HP T1–T5",
                ["Percentage boost to regular Nevron HP. 0% = no boost."] =
                    "Procentowy bonus do HP zwykłych Nevronów. 0% = brak bonusu.",
                ["Speed boost T1–T5"] = "Bonus szybkości T1–T5",
                ["Percentage boost to regular Nevron speed. 0% = no boost."] =
                    "Procentowy bonus do szybkości zwykłych Nevronów. 0% = brak bonusu.",
                ["Attack boost T1–T5"] = "Bonus ataku T1–T5",
                ["Percentage boost to regular Nevron attack. 0% = no boost."] =
                    "Procentowy bonus do ataku zwykłych Nevronów. 0% = brak bonusu.",
                ["Reset boss boosts"] = "Przywróć bonusy bossów",
                ["Restore the default boss boosts."] = "Przywróć domyślne bonusy bossów.",
                ["Percentage boosts over vanilla boss values. Regular Nevron boosts do not affect bosses."] =
                    "Bonusy procentowe względem bazowych wartości bossów. Bonusy zwykłych Nevronów nie dotyczą bossów.",
                ["Boss HP boost T1–T5"] = "Bonus HP bossów T1–T5",
                ["Percentage boost to boss HP for T1–T3. 0% = vanilla."] =
                    "Procentowy bonus do HP bossów dla T1–T3. 0% = bazowo.",
                ["curve"] = "krzywa",
                ["T4–T5 boss HP is controlled by the hidden endgame curve."] =
                    "HP bossów T4–T5 jest kontrolowane przez ukrytą krzywą końcową.",
                ["T4–T5 boss HP uses the hidden endgame curve and has no exposed control."] =
                    "HP bossów T4–T5 korzysta z ukrytej krzywej końcowej i nie ma ujawnionej kontrolki.",
                ["Boss speed boost"] = "Bonus szybkości bossów",
                ["Percentage boost to boss speed. 0% = vanilla."] =
                    "Procentowy bonus do szybkości bossów. 0% = bazowo.",
                ["Extreme T4–T5 boss defense"] = "Ekstremalna obrona bossów T4–T5",
                ["Off uses the regular T4–T5 boss defense curve. On ramps defense from 1× at the T4 floor to 30× the regular curve at the top of T5."] =
                    "Wyłączone używa zwykłej krzywej obrony bossów T4–T5. Włączone zwiększa obronę od 1× na początku T4 do 30× zwykłej krzywej na szczycie T5.",
                ["Expedition Infinity is already running."] = "Expedition Infinity już działa.",
                ["Close the other generator window before starting another build. Two instances would race over the donor cache and the installed seed."] =
                    "Zamknij drugie okno generatora przed rozpoczęciem kolejnego budowania. Dwie instancje kolidowałyby z pamięcią podręczną i zainstalowanym ziarnem.",
            },
            ["ru"] = new(StringComparer.Ordinal)
            {
                ["Randomizer"] = "Случайное",
                ["Balance"] = "Баланс",
                ["Quality of Life"] = "Удобство",
                ["Language"] = "Язык",
                ["Loot sources"] = "Источники добычи",
                ["Build a seed pack for Expedition Infinity."] = "Создайте пакет зерна для Expedition Infinity.",
                ["Core movesets, pictos and weapons are always included. The seed controls loot, stats and encounters."] =
                    "Базовые навыки, пиктосы и оружие всегда включены. Зерно управляет добычей, характеристиками и встречами.",
                ["Setup"] = "Настройки",
                ["Game folder, output folder and profile."] = "Папка игры, папка установки и профиль.",
                ["Game folder"] = "Папка игры",
                ["The folder that contains the \"Sandfall\" folder."] = "Папка, содержащая каталог «Sandfall».",
                ["Usually found automatically. Browse only if needed."] = "Обычно находится автоматически. Выбирайте вручную только при необходимости.",
                ["Choose the game folder manually."] = "Выбрать папку игры вручную.",
                ["Find automatically"] = "Найти автоматически",
                ["Search the usual Steam folders."] = "Искать в обычных папках Steam.",
                ["Install folder"] = "Папка установки",
                ["Where the finished seed pack is installed. The game reads it from its ~mods folder."] =
                    "Куда устанавливается пакет зерна. Игра читает его из папки ~mods.",
                ["Defaults to the game's ~mods folder. Created if missing."] =
                    "По умолчанию папка ~mods игры. Создаётся при необходимости.",
                ["Choose a different install folder."] = "Выбрать другую папку установки.",
                ["Use default"] = "Использовать по умолчанию",
                ["Use the game's standard mod folder (~mods)."] = "Использовать стандартную папку модов игры (~mods).",
                ["Profile"] = "Профиль",
                ["Save or load your seed settings."] = "Сохранить или загрузить настройки зерна.",
                ["Save profile…"] = "Сохранить профиль…",
                ["Save these settings so you can reuse them."] = "Сохранить настройки для повторного использования.",
                ["Load profile…"] = "Загрузить профиль…",
                ["Load a saved settings file."] = "Загрузить сохранённый файл настроек.",
                ["Browse…"] = "Обзор…",
                ["Seed"] = "Зерно",
                ["Same seed and profile give the same results."] = "Одно зерно и профиль дают одинаковый результат.",
                ["Copy this seed to recreate the same pack later."] = "Скопируйте зерно, чтобы позже воссоздать тот же пакет.",
                ["Random"] = "Случайно",
                ["Pick a new random seed."] = "Выбрать новое случайное зерно.",
                ["Copy"] = "Копировать",
                ["Copy the seed."] = "Копировать зерно.",
                ["Build seed pack"] = "Создать пакет зерна",
                ["Build the pack and install it into the game's ~mods folder."] =
                    "Создать пакет и установить его в папку ~mods игры.",
                ["Cancel"] = "Отмена",
                ["Cancel between build stages; the installed pack is not replaced."] =
                    "Отмена между этапами; установленный пакет не заменяется.",
                ["Open install folder"] = "Открыть папку установки",
                ["Open the game's ~mods folder."] = "Открыть папку ~mods игры.",
                ["Open app folder"] = "Открыть папку приложения",
                ["Open the application folder that stores seeds and caches."] =
                    "Открыть папку приложения с зернами и кешем.",
                ["Open seed library"] = "Открыть библиотеку зерен",
                ["Open the folder that keeps every generated seed."] = "Открыть папку со всеми созданными зернами.",
                ["Copy log"] = "Копировать журнал",
                ["Copy the build log for troubleshooting."] = "Копировать журнал сборки для диагностики.",
                ["Clear log"] = "Очистить журнал",
                ["Clear the log window."] = "Очистить окно журнала.",
                ["Ready."] = "Готово.",
                ["Idle."] = "Ожидание.",
                ["Game folder not found."] = "Папка игры не найдена.",
                ["Game folder ready."] = "Папка игры готова.",
                ["That folder does not look like Expedition 33."] = "Эта папка не похожа на Expedition 33.",
                ["Expedition 33 found. Confirm the folder."] = "Expedition 33 найдена. Подтвердите папку.",
                ["The application folder is not writable."] = "Папка приложения недоступна для записи.",
                ["Build cancelled before extraction."] = "Сборка отменена до извлечения.",
                ["Build cancelled. The previously installed pack was not replaced."] =
                    "Сборка отменена. Ранее установленный пакет не заменён.",
                ["Pack failed."] = "Не удалось создать пакет.",
                ["Building. Quit the game completely before packing."] =
                    "Сборка. Полностью закройте игру перед упаковкой.",
                ["Building…"] = "Сборка…",
                ["Extracting…"] = "Извлечение…",
                ["Cancelling after the current stage…"] = "Отмена после текущего этапа…",
                ["Cancelling build; please wait."] = "Отмена сборки; подождите.",
                ["Preparing game data"] = "Подготовка данных игры",
                ["Extracting game data. This can take several minutes; keep the app open."] =
                    "Извлечение данных игры. Это может занять несколько минут; не закрывайте приложение.",
                ["Game data ready"] = "Данные игры готовы",
                ["Game data extraction failed."] = "Не удалось извлечь данные игры.",
                ["Game data extraction skipped. The first build will ask again."] =
                    "Извлечение пропущено. При первой сборке запрос появится снова.",
                ["Language changed."] = "Язык изменён.",
                ["Type a seed or roll a new one."] = "Введите зерно или создайте новое.",
                ["Type a number from 0 to 99,999,999."] = "Введите число от 0 до 99 999 999.",
                ["Pick a random seed."] = "Выбрать случайное зерно.",
                ["Name of the pack that will be generated."] = "Имя создаваемого пакета.",
                ["Your last 20 builds."] = "Ваши последние 20 сборок.",
                ["Seed history"] = "История зерен",
                ["Recent builds, newest first."] = "Последние сборки, сначала новые.",
                ["Restore seed + profile"] = "Восстановить зерно + профиль",
                ["Reload the seed and all settings from the selected build."] =
                    "Загрузить зерно и все настройки выбранной сборки.",
                ["The seed controls loot, stats and encounters. Core content is always included."] =
                    "Зерно управляет добычей, характеристиками и встречами. Базовый контент всегда включён.",
                ["Choose what the seed can change. Keys, journals, music and quest items stay vanilla."] =
                    "Выберите, что может менять зерно. Ключи, журналы, музыка и предметы заданий остаются обычными.",
                ["Chests and pickups"] = "Сундуки и предметы",
                ["Pictos and weapons found in chests and pickups."] = "Пиктосы и оружие из сундуков и предметов.",
                ["Paint Cages"] = "Клетки красок",
                ["Each cage rolls one tier-appropriate picto or weapon."] =
                    "Каждая клетка даёт пиктос или оружие соответствующего уровня.",
                ["Enemy drops"] = "Добыча с врагов",
                ["Regular, elite, boss and overworld drops."] = "Добыча с обычных врагов, элиты, боссов и из мира.",
                ["Merchant inventories"] = "Ассортимент торговцев",
                ["Tier-appropriate shop stock."] = "Товары соответствующего уровня.",
                ["Weapon passives"] = "Пассивы оружия",
                ["Rolls the passive effect on weapons."] = "Случайно выбирает пассивный эффект оружия.",
                ["Monster stats"] = "Характеристики монстров",
                ["Rolls enemy HP, attack, speed and defense."] = "Случайно выбирает HP, атаку, скорость и защиту врагов.",
                ["Weapon stats and elements"] = "Характеристики и элементы оружия",
                ["Rolls weapon attack, free-aim and element."] = "Случайно выбирает атаку, свободное прицеливание и элемент оружия.",
                ["Battle groups"] = "Боевые группы",
                ["Adds extra enemies to regular encounters. Experimental."] =
                    "Добавляет врагов в обычные бои. Экспериментально.",
                ["Re-roll encounters"] = "Перебрасывать встречи",
                ["Picks a fresh encounter each time a battle starts. Experimental."] =
                    "Выбирает новую встречу при каждом начале боя. Экспериментально.",
                ["All off: the seed changes nothing; core content and Balance/QoL still apply."] =
                    "Всё выключено: зерно ничего не меняет; базовый контент и настройки всё равно применяются.",
                ["Loot tuning"] = "Настройка добычи",
                ["Drop rates, shop stock and experimental battle groups."] =
                    "Шансы добычи, товары магазинов и экспериментальные боевые группы.",
                ["Drop rates and shop stock."] = "Шансы добычи и товары магазинов.",
                ["Regular enemy pictos chance"] = "Шанс пиктоса с обычного врага",
                ["Chance a regular enemy drops a picto."] = "Шанс, что обычный враг оставит пиктос.",
                ["Elite enemy pictos chance"] = "Шанс пиктоса с элитного врага",
                ["Chance an elite enemy drops a picto."] = "Шанс, что элитный враг оставит пиктос.",
                ["Perfect catalyst chance"] = "Шанс идеального катализатора",
                ["Rare endgame catalyst. 3.125% is about 1 in 32. Set 0 to disable."] =
                    "Редкий катализатор конца игры. 3,125% — примерно 1 из 32. Установите 0, чтобы отключить.",
                ["Petank Colour of Lumina"] = "Цвет Люмины Петанка",
                ["Lumina awarded by a Petank."] = "Люмина, выдаваемая Петанком.",
                ["Merchant pictos + weapon slots"] = "Слоты пиктосов + оружия у торговца",
                ["Extra gear slots in shops."] = "Дополнительные слоты снаряжения в магазинах.",
                ["Tint shards per type (T1–T4)"] = "Осколки оттенка на тип (T1–T4)",
                ["Tint shards found in early and mid-game chests."] = "Осколки оттенка из сундуков начала и середины игры.",
                ["Chroma Elixir shards (T1–T4)"] = "Осколки эликсира хромы (T1–T4)",
                ["Chroma Elixir shards found in early and mid-game chests."] =
                    "Осколки эликсира хромы из сундуков начала и середины игры.",
                ["Shapes per type (T1–T4)"] = "Формы на тип (T1–T4)",
                ["Shapes found in early and mid-game chests."] = "Формы из сундуков начала и середины игры.",
                ["Max enemies per battle"] = "Макс. врагов в бою",
                ["Upper limit for battle groups. Bosses and scripted fights are excluded."] =
                    "Предел для боевых групп. Боссы и сюжетные бои исключены.",
                ["Battle groups area"] = "Зона боевых групп",
                ["Flying Waters (test area)"] = "Летящие воды (тестовая зона)",
                ["All eligible areas"] = "Все подходящие зоны",
                ["Limit battle groups to the test area or allow every eligible encounter."] =
                    "Ограничить группы тестовой зоной или разрешить все подходящие встречи.",
                ["Chance to add enemies"] = "Шанс добавить врагов",
                ["How often a regular encounter gets extra enemies."] = "Как часто обычная встреча получает дополнительных врагов.",
                ["Shuffle final battle rosters"] = "Перемешивать финальные составы",
                ["Randomize the order of enemies in the final group."] = "Случайный порядок врагов в финальной группе.",
                ["Variants per encounter"] = "Варианты на встречу",
                ["How many alternate groups are prepared for each encounter."] =
                    "Сколько альтернативных групп готовится для каждой встречи.",
                ["Enemy stat bands"] = "Диапазоны характеристик врагов",
                ["Multipliers per tier. 1.0 is vanilla."] = "Множители по уровням. 1,0 — обычное значение.",
                ["Enemy stat min"] = "Мин. характеристика врага",
                ["Enemy stat max"] = "Макс. характеристика врага",
                ["Lowest non-boss stat multiplier for each tier."] = "Наименьший множитель характеристик (кроме боссов) для уровня.",
                ["Highest non-boss stat multiplier for each tier."] = "Наибольший множитель характеристик (кроме боссов) для уровня.",
                ["Boss HP ×"] = "HP босса ×",
                ["Boss HP multiplier for each tier."] = "Множитель HP босса для уровня.",
                ["Attack min"] = "Мин. атака",
                ["Attack max"] = "Макс. атака",
                ["Lowest physical and magical attack multiplier."] = "Наименьший множитель физической и магической атаки.",
                ["Highest physical and magical attack multiplier."] = "Наибольший множитель физической и магической атаки.",
                ["Boss speed ×"] = "Скорость босса ×",
                ["Extra speed multiplier applied to bosses."] = "Дополнительный множитель скорости боссов.",
                ["Reset to Expedition preset"] = "Сбросить к профилю Expedition",
                ["Restore the default stat bands."] = "Восстановить стандартные диапазоны характеристик.",
                ["Adjust difficulty and growth after the seed rolls."] = "Настройте сложность и развитие после выбора зерна.",
                ["Enemy and lumina balance"] = "Баланс врагов и Люмины",
                ["Adjust regular enemy stats by tier"] = "Настраивать характеристики обычных врагов по уровням",
                ["Positive values strengthen regular enemies; negative values weaken them. Bosses and alphas are not affected."] =
                    "Положительные значения усиливают обычных врагов; отрицательные ослабляют. Боссы и альфы не затрагиваются.",
                ["HP adjustment T1–T5"] = "Коррекция HP T1–T5",
                ["Added to normal enemy HP. 0% = vanilla; negative values weaken."] =
                    "Добавляется к HP обычных врагов. 0% = обычное значение; отрицательные значения ослабляют.",
                ["Speed adjustment T1–T5"] = "Коррекция скорости T1–T5",
                ["Added to normal enemy speed. 0% = vanilla; negative values weaken."] =
                    "Добавляется к скорости обычных врагов. 0% = обычное значение; отрицательные значения ослабляют.",
                ["Attack adjustment T1–T5"] = "Коррекция атаки T1–T5",
                ["Added to normal enemy attack. 0% = vanilla; negative values weaken."] =
                    "Добавляется к атаке обычных врагов. 0% = обычное значение; отрицательные значения ослабляют.",
                ["Reduce regular lumina drops"] = "Уменьшить Люмину с обычных врагов",
                ["Chests, enemy drops and shops only. Dialogue rewards are untouched."] =
                    "Только сундуки, добыча и магазины. Награды из диалогов не меняются.",
                ["Lumina multiplier"] = "Множитель Люмины",
                ["0.5 halves regular lumina drops. Dialogue rewards are not changed."] =
                    "0,5 уменьшает обычную Люмину вдвое. Награды из диалогов не меняются.",
                ["Progression"] = "Развитие",
                ["Level and stat growth."] = "Уровни и рост характеристик.",
                ["Character level cap"] = "Предел уровня персонажей",
                ["Maximum character level."] = "Максимальный уровень персонажа.",
                ["Attribute point cap"] = "Предел очков характеристик",
                ["Maximum points in a single attribute."] = "Максимум очков в одной характеристике.",
                ["XP per extra level (100+)"] = "Опыт за дополнительный уровень (100+)",
                ["XP needed for each level above 99."] = "Опыт, необходимый для каждого уровня выше 99.",
                ["Attribute points per level"] = "Очки характеристик за уровень",
                ["Attribute points gained per level."] = "Очки характеристик, получаемые за уровень.",
                ["Skill-tree points per level"] = "Очки дерева навыков за уровень",
                ["Skill points gained per level."] = "Очки навыков, получаемые за уровень.",
                ["Lumina points per level"] = "Очки Люмины за уровень",
                ["Lumina gained per level."] = "Люмина, получаемая за уровень.",
                ["Player power"] = "Сила игрока",
                ["Multipliers on the normal level curves. 1.0 is intended."] =
                    "Множители обычных кривых уровней. 1,0 — задуманное значение.",
                ["Max HP ×"] = "Макс. HP ×",
                ["Physical Attack ×"] = "Физическая атака ×",
                ["Speed ×"] = "Скорость ×",
                ["Critical Rate ×"] = "Шанс крита ×",
                ["Defense ×"] = "Защита ×",
                ["Difficulty"] = "Сложность",
                ["Dodge and parry timing."] = "Тайминг уклонения и парирования.",
                ["Use tuned windows"] = "Использовать настроенные окна",
                ["Shorter windows make dodging and parrying harder."] = "Короткие окна усложняют уклонение и парирование.",
                ["Expedition (0.165 / 0.1125)"] = "Expedition (0,165 / 0,1125)",
                ["Tighter dodge and parry windows."] = "Более короткие окна уклонения и парирования.",
                ["Normal (0.22 / 0.15)"] = "Обычные (0,22 / 0,15)",
                ["Vanilla dodge and parry windows."] = "Стандартные окна уклонения и парирования.",
                ["Dodge window (seconds)"] = "Окно уклонения (секунды)",
                ["How long a dodge input is accepted."] = "Как долго принимается уклонение.",
                ["Parry window (seconds)"] = "Окно парирования (секунды)",
                ["How long a parry input is accepted."] = "Как долго принимается парирование.",
                ["Weapon progression"] = "Развитие оружия",
                ["Scale weapon drop levels"] = "Масштабировать уровни выпадающего оружия",
                ["Extends weapon drops beyond the vanilla level range."] = "Расширяет выпадение оружия за обычный диапазон.",
                ["Max weapon drop level"] = "Макс. уровень выпадающего оружия",
                ["Weapon drops scale from 1 to this level. Pictos stay 1–33."] =
                    "Оружие выпадает от 1 до этого уровня. Пиктосы остаются 1–33.",
                ["Fixed core tuning"] = "Фиксированные базовые настройки",
                ["Combat basics stay at the intended Expedition Infinity values."] =
                    "Основы боя остаются на задуманных значениях Expedition Infinity.",
                ["AP, turns, burn and critical damage are fixed."] = "AP, ходы, горение и критический урон фиксированы.",
                ["Convenience changes. Core balance is unchanged."] = "Удобства. Базовый баланс не меняется.",
                ["Picto mastery"] = "Мастерство пиктосов",
                ["Instant Picto mastery"] = "Мгновенное мастерство пиктосов",
                ["Instant mastery on pickup (always on)"] = "Мгновенное мастерство при получении (всегда включено)",
                ["A found picto is mastered immediately. It does not grant pictos you have not found."] =
                    "Найденный пиктос сразу осваивается. Он не даёт пиктосы, которые вы не находили.",
                ["Battle proc cards"] = "Карты прока в бою",
                ["Max battle proc cards (0 = vanilla)"] = "Макс. карт эффектов в бою (0 = обычно)",
                ["Limits how many picto/passive cards can stack on screen during battle. 0 leaves it vanilla."] =
                    "Ограничивает число карт пиктосов/пассивов на экране в бою. 0 оставляет как обычно.",
                ["Tutorials and stains"] = "Обучение и оттенки",
                ["Skip tutorials"] = "Пропускать обучение",
                ["Tutorial fights are replaced by their normal enemy versions."] =
                    "Обучающие бои заменяются обычными версиями врагов.",
                ["Restore Light-stain substitution for Lune"] = "Вернуть замену оттенка Света для Люн",
                ["A held Light stain can cover another missing element. Elemental Genesis, Cataclysm and Mayhem stay strict."] =
                    "Удерживаемый оттенок Света может заменить другой недостающий элемент. Генезис, Катаклизм и Mayhem остаются строгими.",
                ["Weapon upgrades"] = "Улучшения оружия",
                ["Bulk upgrade"] = "Массовое улучшение",
                ["Hold to upgrade multiple levels at once, up to the current catalyst tier limit."] =
                    "Удерживайте, чтобы улучшить несколько уровней сразу, до текущего предела уровня катализатора.",
                ["Levels per hold"] = "Уровней за удержание",
                ["Maximum levels added by one hold."] = "Максимум уровней за одно удержание.",
                ["Boss movesets"] = "Наборы приёмов боссов",
                ["Mask Keeper keeps the full moveset"] = "Хранитель Масок сохраняет полный набор приёмов",
                ["The Mask Keeper keeps its mask moves after the sub-bosses are defeated."] =
                    "Хранитель Масок сохраняет приёмы масок после победы над суб-боссами.",
                ["Sirène keeps her full moveset"] = "Сирена сохраняет полный набор приёмов",
                ["Sirène keeps her Tisseur-synergy moves after Tisseur is defeated."] =
                    "Сирена сохраняет приёмы синергии с Тиссёром после его поражения.",
                ["Five-member party: always on."] = "Отряд из пяти: всегда включён.",
                ["Build convenience"] = "Удобство сборки",
                ["Open the install folder after a build"] = "Открывать папку установки после сборки",
                ["Open the game's ~mods folder when the build finishes."] = "Открывать папку ~mods игры по завершении сборки.",
                ["Open the spoiler after a build"] = "Открывать спойлер после сборки",
                ["Open the offline spoiler page in your browser."] = "Открыть офлайн-страницу спойлеров в браузере.",
                ["Language of the pack and the app."] = "Язык пакета и приложения.",
                ["Changes the pack and app language."] = "Меняет язык пакета и приложения.",
                ["Choose the folder that contains the \"Sandfall\" folder."] = "Выберите папку, содержащую каталог «Sandfall».",
                ["We couldn't find Expedition 33 in the usual Steam folders."] =
                    "Не удалось найти Expedition 33 в обычных папках Steam.",
                ["Typical location: Steam → steamapps → common → Expedition 33"] =
                    "Обычное расположение: Steam → steamapps → common → Expedition 33",
                ["Quit"] = "Выйти",
                ["Choose folder…"] = "Выбрать папку…",
                ["Find Expedition 33"] = "Найти Expedition 33",
                ["Expedition 33 found"] = "Expedition 33 найдена",
                ["Use this folder"] = "Использовать эту папку",
                ["Choose another folder…"] = "Выбрать другую папку…",
                ["By default, packs install to the game's ~mods folder. You can change both folders later in Setup."] =
                    "По умолчанию пакеты устанавливаются в папку ~mods игры. Обе папки можно изменить позже в Настройках.",
                ["First-time setup"] = "Первичная настройка",
                ["Finish game data setup"] = "Завершить настройку данных игры",
                ["Expedition Infinity needs some data from your game before it can build seeds. This is a one-time step."] =
                    "Expedition Infinity нужны данные из игры, прежде чем создавать зерна. Это однократный шаг.",
                ["Most game data is already prepared. {0} of {1} item(s) still need to be read from your game."] =
                    "Большая часть данных игры уже готова. Осталось прочитать {0} из {1} элементов.",
                ["• About 1.2 GB of files"] = "• Около 1,2 ГБ файлов",
                ["• Usually 5–15 minutes, depending on the drive"] = "• Обычно 5–15 минут, в зависимости от диска",
                ["• Stored inside this application's folder"] = "• Хранится в папке этого приложения",
                ["• Quit Expedition 33 before continuing"] = "• Закройте Expedition 33 перед продолжением",
                ["• No game files are modified"] = "• Файлы игры не изменяются",
                ["• Only the missing items are read"] = "• Читаются только недостающие элементы",
                ["• Usually quick"] = "• Обычно быстро",
                ["Saved to"] = "Сохраняется в",
                ["If you cancel, the generator will ask again before the first build or the next launch. You can still choose a seed, profile and language."] =
                    "Если отменить, генератор снова спросит перед первой сборкой или следующим запуском. Вы всё равно можете выбрать зерно, профиль и язык.",
                ["Start extraction"] = "Начать извлечение",
                ["Finish setup"] = "Завершить настройку",
                ["Not now"] = "Не сейчас",
                ["Existing game data is reused; nothing is downloaded again."] =
                    "Существующие данные игры используются повторно; ничего не скачивается заново.",
                ["This is a one-time setup step."] = "Это однократный шаг настройки.",
                ["Preparing game data…"] = "Подготовка данных игры…",
                ["Continue"] = "Продолжить",
                ["Discard custom changes?"] = "Отменить пользовательские изменения?",
                ["Loading {0} will replace your custom settings."] = "Загрузка {0} заменит ваши пользовательские настройки.",
                ["Custom"] = "Пользовательский",
                ["Gentler"] = "Мягче",
                ["Brutal"] = "Жестче",
                ["Loot only"] = "Только добыча",
                ["Stats only"] = "Только характеристики",
                ["Regular enemy stats are 25% lower than the Expedition Infinity preset; normal defense windows."] =
                    "Характеристики обычных врагов на 25% ниже, чем в профиле Expedition Infinity; обычные окна защиты.",
                ["Enemy HP is doubled and speed/attack are 50% higher than the Expedition Infinity preset; half lumina."] =
                    "HP врагов удвоено, а скорость и атака на 50% выше, чем в профиле Expedition Infinity; половина Люмины.",
                ["Standard combat; only loot rolls change."] =
                    "Обычный бой; меняются только результаты добычи.",
                ["Standard combat; only enemy and weapon stats change."] =
                    "Обычный бой; меняются только характеристики врагов и оружия.",
                ["Your own custom settings."] = "Ваши собственные настройки.",
                ["Standard Expedition Infinity combat and progression."] =
                    "Стандартный бой и развитие Expedition Infinity.",
                ["Checking settings"] = "Проверка настроек",
                ["Preparing tools"] = "Подготовка инструментов",
                ["Building core content"] = "Сборка базового контента",
                ["Applying balance"] = "Применение баланса",
                ["Applying quality of life"] = "Применение удобств",
                ["Rolling the seed"] = "Выбор зерна",
                ["Applying language"] = "Применение языка",
                ["Writing the spoiler"] = "Запись спойлера",
                ["Packing the seed"] = "Упаковка зерна",
                ["Checking the pack"] = "Проверка пакета",
                ["Installing"] = "Установка",
                ["Done"] = "Готово",
                ["enemy data"] = "данные врагов",
                ["weapon and gear data"] = "данные оружия и снаряжения",
                ["skill data"] = "данные навыков",
                ["picto data"] = "данные пиктосов",
                ["loot and merchant data"] = "данные добычи и торговцев",
                ["text and localization"] = "тексты и локализация",
                ["battle data"] = "данные боев",
                ["character data"] = "данные персонажей",
                ["game blueprints"] = "чертежи игры",
                ["game data assets"] = "ресурсы игры",
                ["game assets"] = "ресурсы игры",
                ["game data"] = "данные игры",
                ["Extracting {0}…"] = "Извлечение: {0}…",
                ["{0} of {1} tasks complete ({2}%)"] = "{0} из {1} задач выполнено ({2}%)",
                ["This is a one-time setup. Keep this window open."] = "Это однократная настройка. Не закрывайте окно.",
                ["Checking which game files are needed…"] = "Проверка необходимых файлов игры…",
                ["That folder does not look like Expedition 33. Choose the folder that contains the \"Sandfall\" folder."] =
                    "Эта папка не похожа на Expedition 33. Выберите папку, содержащую каталог «Sandfall».",
                ["Choose the Expedition 33 game folder"] = "Выбрать папку игры Expedition 33",
                ["Choose the install folder"] = "Выбрать папку установки",
                ["Save Expedition Infinity profile"] = "Сохранить профиль Expedition Infinity",
                ["Load Expedition Infinity profile"] = "Загрузить профиль Expedition Infinity",
                ["Expedition Infinity profile"] = "Профиль Expedition Infinity",
                ["The seed controls loot, Nevron stats and encounters. Core content is always included."] =
                    "Зерно управляет добычей, характеристиками Невронов и встречами. Базовый контент всегда включён.",
                ["Loot"] = "Добыча",
                ["Loot options"] = "Параметры добычи",
                ["Nevrons"] = "Невроны",
                ["Nevron options"] = "Параметры Невронов",
                ["Choose which loot sources the seed changes. Keys, journals, music and quest items stay vanilla."] =
                    "Выберите источники добычи, которые меняет зерно. Ключи, журналы, музыка и предметы заданий остаются обычными.",
                ["Nevron drops"] = "Добыча с Невронов",
                ["Regular, elite, boss and overworld Nevron drops."] =
                    "Добыча с обычных Невронов, элиты, боссов и из мира.",
                ["Drop rates, weapon rolls and loot quantities."] = "Шансы добычи, параметры оружия и количество добычи.",
                ["Regular Nevron picto chance"] = "Шанс пиктоса с обычного Неврона",
                ["Chance a regular Nevron drops a picto."] = "Шанс, что обычный Неврон оставит пиктос.",
                ["Elite Nevron picto chance"] = "Шанс пиктоса с элитного Неврона",
                ["Chance an elite Nevron drops a picto."] = "Шанс, что элитный Неврон оставит пиктос.",
                ["Chests, Nevron drops and shops only. Dialogue rewards are untouched."] =
                    "Только сундуки, добыча с Невронов и магазины. Награды из диалогов не меняются.",
                ["Choose whether Nevron stats are rerolled by the seed."] =
                    "Выберите, пересоздаются ли характеристики Невронов зерном.",
                ["Nevron stats"] = "Характеристики Невронов",
                ["Rolls Nevron HP, attack, speed and defense."] =
                    "Случайно выбирает HP, атаку, скорость и защиту Невронов.",
                ["Turned off: the seed leaves Nevron stats at their rolled band defaults."] =
                    "Выключено: зерно оставляет характеристики Невронов на значениях диапазона.",
                ["Rolled stat bands, followed by an optional extra adjustment."] =
                    "Случайные диапазоны характеристик, затем необязательная дополнительная коррекция.",
                ["Rolled bands"] = "Случайные диапазоны",
                ["Extra adjustment"] = "Дополнительная коррекция",
                ["Nevron stat min"] = "Мин. характеристика Неврона",
                ["Nevron stat max"] = "Макс. характеристика Неврона",
                ["Reset bands to Expedition preset"] = "Сбросить диапазоны к профилю Expedition",
                ["Restore the default Nevron stat bands."] = "Восстановить стандартные диапазоны характеристик Невронов.",
                ["Adjust regular Nevron stats by tier"] = "Настраивать характеристики обычных Невронов по уровням",
                ["Positive values strengthen regular Nevrons; negative values weaken them. Bosses and alphas are not affected."] =
                    "Положительные значения усиливают обычных Невронов; отрицательные ослабляют. Боссы и альфы не затрагиваются.",
                ["Added after the roll. 0% = no extra adjustment."] =
                    "Добавляется после выбора. 0% = без дополнительной коррекции.",
                ["All sources off: the seed changes nothing; core content and Balance/QoL still apply."] =
                    "Все источники выключены: зерно ничего не меняет; базовый контент и настройки всё равно применяются.",
                ["Adjust progression, attributes, boss movesets and parry timing after the seed rolls."] =
                    "Настройте развитие, характеристики, наборы приёмов боссов и время парирования после выбора зерна.",
                ["Attributes"] = "Характеристики",
                ["Multipliers on the character's attribute curves. 1.0 is intended."] =
                    "Множители кривых характеристик персонажа. 1,0 — задуманное значение.",
                ["Parry timings"] = "Окна парирования",
                ["Dodge and parry windows."] = "Окна уклонения и парирования.",
                ["These values are fixed in the generator."] = "Эти значения фиксированы в генераторе.",
                ["Easy"] = "Лёгкий",
                ["Enemy stat bonuses are 25% weaker than the Expedition Infinity preset; normal defense windows. Includes instant Picto mastery."] =
                    "Бонусы характеристик Невронов на 25% слабее, чем в профиле Expedition Infinity; обычные окна защиты. Включает мгновенное мастерство пиктосов.",
                ["Enemy HP bonus is doubled and speed/attack bonuses are 50% stronger than the Expedition Infinity preset; half Lumina."] =
                    "Бонус HP удвоен, а бонусы скорости/атаки на 50% сильнее, чем в профиле Expedition Infinity; половина Люмины.",
                ["Adjust Lumina drops"] = "Настроить Люмину",
                ["× (0.5 = half, 2 = double)"] = "× (0,5 = половина, 2 = вдвое)",
                ["0.5 halves regular Lumina drops; 2 doubles them. Dialogue rewards are not changed."] =
                    "0,5 уменьшает обычную Люмину вдвое; 2 удваивает её. Награды из диалогов не меняются.",
                ["Max AP 21 · 3 consecutive turns · max Foretell 30 · max shields 5 · burn 0.075 / 0.01 / 3 turns · crit addend 1.5 with crit-rate scaling. These values are fixed in the generator."] =
                    "Макс. ОД 21 · 3 хода подряд · макс. предвестье 30 · макс. 5 щитов · ожог 0,075 / 0,01 / 3 хода · бонус крита 1,5 с масштабированием от шанса крита. Эти значения фиксированы в генераторе.",
                ["Regular Nevrons"] = "Обычные Невроны",
                ["Bosses"] = "Боссы",
                ["Rolled stat bands and extra adjustments for regular Nevrons."] =
                    "Случайные диапазоны и дополнительные коррекции для обычных Невронов.",
                ["Boss stat bands. Regular Nevron adjustments do not affect bosses."] =
                    "Диапазоны характеристик боссов. Коррекции обычных Невронов не влияют на боссов.",
                ["Reset regular bands"] = "Сбросить обычные диапазоны",
                ["Restore the default regular Nevron stat bands."] =
                    "Восстановить стандартные диапазоны характеристик обычных Невронов.",
                ["Reset boss bands"] = "Сбросить диапазоны боссов",
                ["Restore the default boss stat bands."] =
                    "Восстановить стандартные диапазоны характеристик боссов.",
                ["Light stains"] = "Пигменты Света",
                ["Open the spoiler folder after a build"] =
                    "Открывать папку спойлера после сборки",
                ["Open the folder that contains the generated spoiler page."] =
                    "Открыть папку с созданной страницей спойлера.",
                ["Reset regular boosts"] = "Сбросить обычные бонусы",
                ["Restore the default regular Nevron boosts."] = "Восстановить стандартные бонусы обычных Невронов.",
                ["Percentage boosts applied after the seed roll. 0% = no boost."] =
                    "Процентные бонусы применяются после выбора. 0% = без бонуса.",
                ["Apply regular Nevron percentage boosts"] = "Применять процентные бонусы обычных Невронов",
                ["Positive percentages strengthen regular Nevrons; negative percentages weaken them. Bosses and alphas are not affected."] =
                    "Положительные проценты усиливают обычных Невронов; отрицательные ослабляют. Боссы и альфы не затрагиваются.",
                ["HP boost T1–T5"] = "Бонус HP T1–T5",
                ["Percentage boost to regular Nevron HP. 0% = no boost."] =
                    "Процентный бонус к HP обычных Невронов. 0% = без бонуса.",
                ["Speed boost T1–T5"] = "Бонус скорости T1–T5",
                ["Percentage boost to regular Nevron speed. 0% = no boost."] =
                    "Процентный бонус к скорости обычных Невронов. 0% = без бонуса.",
                ["Attack boost T1–T5"] = "Бонус атаки T1–T5",
                ["Percentage boost to regular Nevron attack. 0% = no boost."] =
                    "Процентный бонус к атаке обычных Невронов. 0% = без бонуса.",
                ["Reset boss boosts"] = "Сбросить бонусы боссов",
                ["Restore the default boss boosts."] = "Восстановить стандартные бонусы боссов.",
                ["Percentage boosts over vanilla boss values. Regular Nevron boosts do not affect bosses."] =
                    "Процентные бонусы к базовым значениям боссов. Бонусы обычных Невронов не влияют на боссов.",
                ["Boss HP boost T1–T5"] = "Бонус HP боссов T1–T5",
                ["Percentage boost to boss HP for T1–T3. 0% = vanilla."] =
                    "Процентный бонус к HP боссов для T1–T3. 0% = базово.",
                ["curve"] = "кривая",
                ["T4–T5 boss HP is controlled by the hidden endgame curve."] =
                    "HP боссов T4–T5 управляется скрытой кривой конца игры.",
                ["T4–T5 boss HP uses the hidden endgame curve and has no exposed control."] =
                    "HP боссов T4–T5 использует скрытую кривую конца игры и не имеет открытого элемента управления.",
                ["Boss speed boost"] = "Бонус скорости боссов",
                ["Percentage boost to boss speed. 0% = vanilla."] =
                    "Процентный бонус к скорости боссов. 0% = базово.",
                ["Extreme T4–T5 boss defense"] = "Экстремальная защита боссов T4–T5",
                ["Off uses the regular T4–T5 boss defense curve. On ramps defense from 1× at the T4 floor to 30× the regular curve at the top of T5."] =
                    "Выключено — обычная кривая защиты боссов T4–T5. Включено — защита растёт от 1× на начале T4 до 30× обычной кривой на вершине T5.",
                ["Expedition Infinity is already running."] = "Expedition Infinity уже запущена.",
                ["Close the other generator window before starting another build. Two instances would race over the donor cache and the installed seed."] =
                    "Закройте другое окно генератора перед новой сборкой. Два экземпляра будут конфликтовать из-за кеша и установленного зерна.",
                ["Game data ready."] = "Данные игры готовы.",
                ["Game data ready — {0} extraction task(s) completed."] = "Данные игры готовы — выполнено задач: {0}.",
                ["Game files not found in that folder."] = "Файлы игры не найдены в этой папке.",
                ["Install folder is not writable."] = "Папка установки недоступна для записи.",
                ["Install folder (created if missing)"] = "Папка установки (создаётся при необходимости)",
                ["Expedition 33 game folder"] = "Папка игры Expedition 33",
                ["New seed"] = "Новое зерно",
                ["Profile loaded."] = "Профиль загружен.",
                ["Profile saved."] = "Профиль сохранён.",
                ["Restored seed {0}."] = "Зерно {0} восстановлено.",
                ["Loading…"] = "Загрузка…",
                ["Could not open the generator."] = "Не удалось открыть генератор.",
                ["Pack: {0}"] = "Пакет: {0}",
                ["× (0.5 = half)"] = "× (0,5 = половина)",
                ["Settings validation failed."] = "Проверка настроек не удалась.",
                ["Starting…"] = "Запуск…",
                ["This one-time setup reads game data. Later builds skip it."] = "Эта однократная настройка читает данные игры. Следующие сборки её пропускают.",
                ["vanilla 0.15"] = "обычное 0,15",
                ["vanilla 0.22"] = "обычное 0,22",
                ["Packed {0} ({1} edits) into {2}"] = "Пакет {0} создан ({1} изменений) в {2}",
                ["Packed {0} ({1} edits); library {2}; installed in {3}"] = "Пакет {0} создан ({1} изменений); библиотека {2}; установлен в {3}",
                ["Max AP 21 · 3 consecutive turns · max Foretell 30 · max shields 5 · burn 0.075 / 0.01 / 3 turns · crit addend 1.5 with crit-rate scaling. Use the full editor for experimental changes."] =
                    "Макс. AP 21 · 3 хода подряд · макс. Предвидение 30 · макс. щиты 5 · горение 0,075 / 0,01 / 3 хода · бонус крита 1,5 с масштабированием от шанса крита. Для экспериментов используйте полный редактор.",
            },
        };
}
