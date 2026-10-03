namespace LastLight.Maze
{
    /// <summary>
    /// MazeBuilder 참조가 필요한 함정 등이 구현. 프리팹 에셋은 씬에만 존재하는 MazeBuilder를
    /// 인스펙터로 직접 참조할 수 없으므로(저장 시 자동으로 비워짐), 배치하는 쪽(MazeBuilder)이
    /// 오브젝트 풀에서 꺼낸 직후 런타임에 이 메서드로 자기 자신을 넣어준다.
    /// </summary>
    public interface IRequiresMaze
    {
        void InjectMaze(MazeBuilder maze);
    }
}