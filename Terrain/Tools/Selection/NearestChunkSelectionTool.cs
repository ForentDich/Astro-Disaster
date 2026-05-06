public static class NearestChunkSelectionTool
{
    public static void EnsureCapacity(ref int[] selectedEntityIds, ref float[] selectedDistances, int capacity)
    {
        if (selectedEntityIds == null || selectedEntityIds.Length < capacity)
        {
            selectedEntityIds = new int[capacity];
            selectedDistances = new float[capacity];
        }
    }

    public static void TryInsertNearest(ref int selectedCount, int[] selectedEntityIds, float[] selectedDistances, int entityId, float distance, int max)
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
}
