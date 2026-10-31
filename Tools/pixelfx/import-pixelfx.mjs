#!/usr/bin/env node
// pixelfx(https://github.com/patch-gg/pixelfx, MIT)에서 이 게임이 쓰는 효과의 스프라이트 시트를 뽑아
// Assets/KitchenDefense/Resources/PixelFx/ 에 넣는다. Unity로 돌아가면 자동으로 임포트되고
// (Editor/PixelFxImport가 픽셀 아트 설정을 맞춤) 게임이 도형 연출 대신 이 효과들을 쓴다.
//
//   git clone https://github.com/patch-gg/pixelfx ../pixelfx
//   node Tools/pixelfx/import-pixelfx.mjs ../pixelfx
//
// 이 스크립트는 pixelfx의 tools/render.mjs를 실행한다. 실행 전에 그 저장소 코드를 확인할 것.
// 필요: Node.js 18 이상.

import { spawnSync } from "node:child_process";
import { copyFileSync, existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

// 게임 연출 → pixelfx 효과 (이름은 PixelFxLibrary.cs의 상수와 같아야 한다)
const EFFECTS = [
  "impacts/hit-spark",     // 식칼 타격
  "spawn/poof",            // 처치
  "ice/snowflake-burst",   // 냉동 막타
  "water/splash",          // 가마솥 삶기
  "pickups/collect-burst", // 예약 재료 획득
  "ui/heart-lost",         // 창고가 털림(내구도 -1)
  "status/burning",        // 화상 중인 몬스터(반복)
  "status/frozen",         // 감속 중인 몬스터(반복)
];

const pixelfx = resolve(process.argv[2] ?? "../pixelfx");
const render = join(pixelfx, "tools", "render.mjs");
if (!existsSync(render)) {
  console.error(`pixelfx를 찾지 못했습니다: ${render}\n사용법: node Tools/pixelfx/import-pixelfx.mjs <pixelfx 저장소 경로>`);
  process.exit(1);
}

const project = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const dest = join(project, "Assets", "KitchenDefense", "Resources", "PixelFx");
const out = mkdtempSync(join(tmpdir(), "kd-pixelfx-"));

const result = spawnSync(process.execPath, [render, ...EFFECTS, "--sheet", "--no-actor", "--bg", "none", "--out", out], {
  cwd: pixelfx,
  stdio: "inherit",
});
if (result.status !== 0) {
  console.error("pixelfx 렌더링에 실패했습니다.");
  process.exit(result.status ?? 1);
}

mkdirSync(dest, { recursive: true });
for (const id of EFFECTS) {
  const name = id.split("/")[1];
  for (const ext of [".png", ".json"]) copyFileSync(join(out, id, name + ext), join(dest, name + ext));
}

// MIT 라이선스는 사본과 함께 배포해야 하므로 게임 안(Resources)에 같이 넣는다.
const license = readFileSync(join(pixelfx, "LICENSE"), "utf8");
const notice = existsSync(join(pixelfx, "NOTICE.md")) ? readFileSync(join(pixelfx, "NOTICE.md"), "utf8") : "";
writeFileSync(join(dest, "pixelfx-LICENSE.txt"), `pixelfx - https://github.com/patch-gg/pixelfx\n\n${license}\n\n${notice}`);

console.log(`\n${EFFECTS.length}개 효과를 넣었습니다: ${dest}`);
console.log("Unity로 돌아가면 자동으로 임포트됩니다. 새로 생긴 .meta 파일도 함께 커밋하세요.");
