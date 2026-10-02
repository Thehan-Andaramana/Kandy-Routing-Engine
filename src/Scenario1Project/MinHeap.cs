// Binary min-heap stored in an array, used as the priority queue for Dijkstra.
// The item with the smallest cost is always at index 0.

public class HeapItem
{
    public int locationId;
    public double cost;

    public HeapItem(int locationId, double cost)
    {
        this.locationId = locationId;
        this.cost = cost;
    }
}

public class MinHeap
{
    public HeapItem[] items;
    public int size;
    public int capacity;

    public MinHeap(int capacity)
    {
        this.capacity = capacity;
        this.items = new HeapItem[capacity];
        this.size = 0;
    }

    public bool isEmpty()
    {
        return size == 0;
    }

    public bool isFull()
    {
        return size == capacity;
    }

    public int Count()
    {
        return size;
    }

    // adds an item at the end and moves it up to its place
    public bool Insert(int locationId, double cost)
    {
        if (isFull())
        {
            Console.WriteLine("Heap is full!! Cannot insert location " + locationId);
            return false;
        }

        items[size] = new HeapItem(locationId, cost);

        size++;

        BubbleUp(size - 1);

        return true;
    }

    // move the new item up while it is smaller than its parent
    private void BubbleUp(int index)
    {
        if (index == 0)
        {
            return;
        }

        int parent = (index - 1) / 2;

        if (items[index].cost < items[parent].cost)
        {
            Swap(index, parent);
            BubbleUp(parent);
        }
    }

    // removes and returns the item with the smallest cost
    public HeapItem ExtractMin()
    {
        if (isEmpty())
        {
            Console.WriteLine("Heap is empty!!");
            return null;
        }

        HeapItem min = items[0];

        // the last item replaces the root and then moves down
        items[0] = items[size - 1];
        items[size - 1] = null;
        size--;

        if (size > 0)
        {
            BubbleDown(0);
        }

        return min;
    }

    // swap with the smaller child until both children are bigger
    private void BubbleDown(int index)
    {
        int left = 2 * index + 1;
        int right = 2 * index + 2;
        int smallest = index;

        if (left < size && items[left].cost < items[smallest].cost)
        {
            smallest = left;
        }

        if (right < size && items[right].cost < items[smallest].cost)
        {
            smallest = right;
        }

        if (smallest != index)
        {
            Swap(index, smallest);
            BubbleDown(smallest);
        }
    }

    private void Swap(int a, int b)
    {
        HeapItem temp = items[a];
        items[a] = items[b];
        items[b] = temp;
    }
}
