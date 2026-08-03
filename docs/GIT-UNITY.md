# Unity и git: настройка один раз

Unity-проект хранится в git не так, как обычный код: сцены и префабы — это YAML,
который построчным merge сливать нельзя, а половина репозитория — бинарные
модели и текстуры. Этот файл — про то, что настроить один раз, чтобы работа
в паре не заканчивалась разбором испорченной сцены.

---

## 1. UnityYAMLMerge

`.gitattributes` помечает `.unity`, `.prefab`, `.asset`, `.mat`, `.controller`
и прочий Unity-YAML как `merge=unity`. Драйвер `unity` нужно один раз объявить
в своём `~/.gitconfig` — в репозиторий его положить нельзя, путь у каждого свой.

Инструмент лежит внутри установленного редактора:

| ОС | Путь |
|---|---|
| macOS | `/Applications/Unity/Hub/Editor/<версия>/Unity.app/Contents/Tools/UnityYAMLMerge` |
| Windows | `C:\Program Files\Unity\Hub\Editor\<версия>\Editor\Data\Tools\UnityYAMLMerge.exe` |
| Linux | `~/Unity/Hub/Editor/<версия>/Editor/Data/Tools/UnityYAMLMerge` |

Настройка (macOS, подставьте свою версию):

```bash
git config --global merge.unity.name "UnityYAMLMerge"
git config --global merge.unity.driver '"/Applications/Unity/Hub/Editor/6000.0.32f1/Unity.app/Contents/Tools/UnityYAMLMerge" merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
```

Проверить, что путь верный:

```bash
ls "/Applications/Unity/Hub/Editor/"
```

**Если не настроить — ничего не сломается.** Git не найдёт драйвер `unity`,
молча откатится на обычный merge и пометит конфликт. Это ровно то поведение,
которое было до появления `.gitattributes`.

## 2. `.meta` сливаются вручную

`.meta` — тоже YAML, но в атрибутах у них нет `merge=unity`. Это сделано
намеренно: в `.meta` лежат GUID-ы, по которым Unity связывает ассеты между
собой. Автоматически слитый `.meta` может тихо переназначить ссылку, и это
всплывёт не при merge, а через неделю в виде отвалившегося материала.
Явный конфликт лучше.

При конфликте в `.meta` берите **одну** из версий целиком:

```bash
git checkout --ours Assets/Prefabs/Hero.prefab.meta   # свою
git checkout --theirs Assets/Prefabs/Hero.prefab.meta # входящую
```

## 3. Force Text — уже включено

`ProjectSettings/EditorSettings.asset` содержит `m_SerializationMode: 2`
(Force Text). Без этого сцены сохранялись бы в бинарном виде и никакой merge
не помог бы вообще. Не переключайте обратно.

## 4. Бинарные ассеты

39 моделей `.fbx`, 38 текстур и шрифт лежат в репозитории как обычные файлы —
отсюда 7.5 МБ. В `.gitattributes` они помечены `binary`: git не пытается
искать в них диффы и не трогает переносы строк.

**Про Git LFS.** Для бинарных ассетов он подходит лучше, но перевод существующих
файлов в LFS переписывает историю (`git lfs migrate`), а значит ломает все
существующие клоны и ссылки на коммиты. На нынешнем размере это того не стоит.
Порог, за которым стоит вернуться к вопросу, — примерно 100 МБ репозитория
или заметное замедление `git clone`.

Если решите переходить — делайте это отдельным осознанным шагом, а не попутно:

```bash
git lfs install
git lfs track "*.fbx" "*.png" "*.jpg" "*.ttf"
# и только потом, понимая последствия:
# git lfs migrate import --include="*.fbx,*.png,*.jpg,*.ttf" --everything
```

## 5. Что не коммитим

`.gitignore` закрывает `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`,
`UserSettings/`, файлы IDE и `.csproj`/`.sln` — они генерируются Unity
заново при открытии проекта. Если после клона проект «пустой» — просто
откройте его в Unity нужной версии и дайте импортировать ассеты.
