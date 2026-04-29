using Friflo.Engine.ECS;

// === Состояния видимости ===
public struct SegmentShouldBeVisible : ITag { }   // Должен быть видим (ставит Visibility)
public struct SegmentShouldBeHidden : ITag { }    // Должен быть скрыт (ставит Visibility)
public struct SegmentVisible : ITag { }           // В поле зрения камеры (для рендеринга)
public struct SegmentCulled : ITag { }            // Отсечен фрустумом
public struct SegmentOutOfRange : ITag { }        // Вне радиуса загрузки

// === Состояния активности ===
public struct SegmentActive : ITag { }           // Активен в памяти
public struct SegmentInactive : ITag { }         // Неактивен (готов к удалению)
public struct SegmentLocked : ITag { }           // Заблокирован (в обработке)

// === Состояния для чанков ===
public struct SegmentHasChunks : ITag { }        // Чанки созданы
public struct SegmentNeedsChunks : ITag { }      // Нужно создать чанки