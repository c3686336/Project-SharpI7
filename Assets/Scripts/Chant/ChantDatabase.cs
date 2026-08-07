using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class ChantDatabase : MonoBehaviour
{
    [SerializeField]
    private string fileName = "chants.json";

    private List<ChantSpellData> spells =
        new List<ChantSpellData>();

    public IReadOnlyList<ChantSpellData> Spells => spells;

    public bool IsLoaded { get; private set; }

    private void Awake()
    {
        Load();
    }

    public void Load()
    {
        IsLoaded = false;

        string path = Path.Combine(
            Application.streamingAssetsPath,
            fileName
        );

        if (!File.Exists(path))
        {
            Debug.LogError(
                $"[ChantDatabase] JSON 파일을 찾을 수 없습니다.\n{path}"
            );

            spells.Clear();
            return;
        }

        string json =
            File.ReadAllText(path);

        if (string.IsNullOrWhiteSpace(json))
        {
            Debug.LogError(
                "[ChantDatabase] JSON 파일이 비어 있습니다."
            );

            spells.Clear();
            return;
        }

        ChantSpellDatabaseData data;

        try
        {
            data =
                JsonUtility.FromJson<ChantSpellDatabaseData>(
                    json
                );
        }
        catch (System.Exception e)
        {
            Debug.LogError(
                $"[ChantDatabase] JSON 파싱 실패\n{e}"
            );

            spells.Clear();
            return;
        }

        if (data == null ||
            data.spells == null)
        {
            Debug.LogError(
                "[ChantDatabase] spells 데이터가 없습니다."
            );

            spells.Clear();
            return;
        }

        spells =
            data.spells;

        ValidateDatabase();

        IsLoaded =
            true;

        Debug.Log(
            $"[ChantDatabase] 영창 {spells.Count}개 로드 완료"
        );
    }

    public ChantSpellData GetSpell(
        string spellId
    )
    {
        if (string.IsNullOrEmpty(spellId))
            return null;

        for (
            int i = 0;
            i < spells.Count;
            i++
        )
        {
            ChantSpellData spell =
                spells[i];

            if (spell == null)
                continue;

            if (spell.id == spellId)
                return spell;
        }

        Debug.LogWarning(
            $"[ChantDatabase] 주문을 찾을 수 없습니다: {spellId}"
        );

        return null;
    }

    private void ValidateDatabase()
    {
        HashSet<string> ids =
            new HashSet<string>();

        foreach (
            ChantSpellData spell
            in spells
        )
        {
            if (spell == null)
                continue;

            if (string.IsNullOrWhiteSpace(
                spell.id
            ))
            {
                Debug.LogWarning(
                    "[ChantDatabase] ID가 없는 주문이 있습니다."
                );

                continue;
            }

            if (!ids.Add(spell.id))
            {
                Debug.LogWarning(
                    $"[ChantDatabase] 중복 주문 ID: {spell.id}"
                );
            }

            if (spell.stages == null ||
                spell.stages.Count == 0)
            {
                Debug.LogWarning(
                    $"[ChantDatabase] 단계가 없는 주문: {spell.id}"
                );

                continue;
            }

            foreach (
                ChantStageData stage
                in spell.stages
            )
            {
                if (stage == null)
                    continue;

                if (stage.manaCost < 0)
                {
                    Debug.LogWarning(
                        $"[ChantDatabase] 마나 소비량이 음수입니다. " +
                        $"Spell: {spell.id}, " +
                        $"Level: {stage.chantLevel}"
                    );
                }
            }
        }
    }
}