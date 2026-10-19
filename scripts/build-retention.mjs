// Retained server files keep historical URLs working. They are deliberately not
// passed to the browser worker, whose budget remains current + previous only.
export function retainedBuildFiles(versions) {
  const retained = versions.retained ?? [];
  if (!Array.isArray(retained)) throw new Error('历史构建留存清单无效');
  const pattern = /^[0-9a-f]{32}\.(?:data\.unityweb|framework\.js\.unityweb|wasm\.unityweb|loader\.js)$/;
  const suffixes = ['.data.unityweb', '.framework.js.unityweb', '.wasm.unityweb', '.loader.js'];
  const releaseIds = new Set();
  return retained.flatMap(group => {
    if (!group || typeof group !== 'object' || !/^\d+\.\d+\.\d+$/.test(group.version) ||
        typeof group.releaseId !== 'string' || !group.releaseId.trim() || releaseIds.has(group.releaseId) ||
        !Array.isArray(group.files) || group.files.length !== 4 || new Set(group.files).size !== 4 ||
        group.files.some(file => typeof file !== 'string' || !pattern.test(file)) ||
        suffixes.some(suffix => group.files.filter(file => file.endsWith(suffix)).length !== 1)) {
      throw new Error('历史构建必须记录版本、发布编号与完整四资源');
    }
    releaseIds.add(group.releaseId);
    return group.files;
  });
}
