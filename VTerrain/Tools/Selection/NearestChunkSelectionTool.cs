using Godot;

public static class NearestChunkSelectionTool
{
    public static void EnsureCapacity(ref int[] selectedEntityIds, ref int[] selectedDistances, int capacity)
    {
        if (selectedEntityIds == null || selectedEntityIds.Length < capacity)
        {
            selectedEntityIds = new int[capacity];
            selectedDistances = new int[capacity];
        }
    }

    public static void TryInsertNearest(ref int selectedCount, int[] selectedEntityIds, int[] selectedDistances, int entityId, int distance, int max)
    {
        if (selectedCount < max)
        {
            int insertIndex = selectedCount;
            while (insertIndex > 0 && distance < selectedDistances[insertIndex - 1])
            {
                selectedEntityIds[insertIndex] = selectedEntityIds[insertIndex - 1];
                selectedDistances[insertIndex] = selectedDistances[insertIndex - 1];
                insertIndex--;
            }

            selectedEntityIds[insertIndex] = entityId;
            selectedDistances[insertIndex] = distance;
            selectedCount++;
            return;
        }

        if (selectedCount == 0)
            return;

        int worstIndex = selectedCount - 1;
        if (distance >= selectedDistances[worstIndex])
            return;

        int idx = worstIndex;
        while (idx > 0 && distance < selectedDistances[idx - 1])
        {
            selectedEntityIds[idx] = selectedEntityIds[idx - 1];
            selectedDistances[idx] = selectedDistances[idx - 1];
            idx--;
        }

        selectedEntityIds[idx] = entityId;
        selectedDistances[idx] = distance;
    }

    public static (int x, int z) GetViewerChunkCoords(Node3D viewer, int chunkSize)
    {
        if (viewer == null)
            return (0, 0);

        Vector3 worldPos = viewer.GlobalPosition;
        return (
            Mathf.FloorToInt(worldPos.X / chunkSize),
            Mathf.FloorToInt(worldPos.Z / chunkSize)
        );
    }
}
