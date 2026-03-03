using Friflo.Engine.ECS;

// === Состояния видимости ===
public struct SegmentShouldBeVisible : ITag { }   // Должен быть видим (ставит Visibility)
public struct SegmentShouldBeHidden : ITag { }    // Должен быть скрыт (ставит Visibility)
public struct SegmentVisible : ITag { }           // В поле зрения камеры (для рендеринга)
public struct SegmentCulled : ITag { }            // Отсечен фрустумом
public struct SegmentOutOfRange : ITag { }        // Вне радиуса загрузки

// === Состояния данных ===
public struct SegmentHasHeightmap : ITag { }      // Есть данные высот
public struct SegmentHasBiomes : ITag { }         // Есть данные биомов
public struct SegmentHasResources : ITag { }      // Есть данные ресурсов
public struct SegmentDataReady : ITag { }         // Все данные готовы
public struct SegmentDataDirty : ITag { }         // Данные изменены (нужно сохранить)
public struct SegmentDataClean : ITag { }         // Данные синхронизированы с диском

// === Состояния загрузки/генерации ===
public struct SegmentNeedsLoad : ITag { }         // Нужно загрузить с диска
public struct SegmentNeedsGenerate : ITag { }     // Нужно сгенерировать (нет файла)
public struct SegmentNeedsSave : ITag { }         // Нужно сохранить на диск
public struct SegmentLoading : ITag { }           // В процессе загрузки
public struct SegmentGenerating : ITag { }        // В процессе генерации
public struct SegmentSaving : ITag { }           // В процессе сохранения

// === Состояния активности ===
public struct SegmentActive : ITag { }           // Активен в памяти
public struct SegmentInactive : ITag { }         // Неактивен (готов к удалению)
public struct SegmentLocked : ITag { }           // Заблокирован (в обработке)

// === Состояния для чанков ===
public struct SegmentHasChunks : ITag { }        // Чанки созданы
public struct SegmentNeedsChunks : ITag { }      // Нужно создать чанки