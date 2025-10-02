public enum StoryState
{
    // 초기화
    Game_Start_Screen,
    Player_Name_Input,
    Intro_Meet_Dungddangi,

    // 마무리
    Ending_With_Dungddangi,
    Game_Ended,

    Camera_Standby, // 카메라 켜기 UI 노출
    Camera_Scanning, // AR 추적 활성화, 이미지 스캔 대기
    Dialogue_Running, // UI+대사 진행
}
