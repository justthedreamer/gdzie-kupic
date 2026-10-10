import { describe, it, expect } from 'vitest'
import { FIRST_NAME_MAX_LENGTH, firstNameProblem, normalizeFirstName } from '~/utils/firstName'

describe('normalizeFirstName', () => {
  it('trims and collapses whitespace', () => {
    expect(normalizeFirstName('  Anna   Maria ')).toBe('Anna Maria')
  })

  it('returns an empty string for blank input', () => {
    expect(normalizeFirstName('   ')).toBe('')
  })
})

describe('firstNameProblem', () => {
  it.each(['Anna', 'Żaneta', 'Jean-Luc', "D'Arcy", 'Anna Maria', 'Zoë', '  Ewa  '])('accepts %s', (name) => {
    expect(firstNameProblem(name)).toBeNull()
  })

  it('treats an empty name as "no name"', () => {
    expect(firstNameProblem('')).toBeNull()
    expect(firstNameProblem('   ')).toBeNull()
  })

  it.each(['Anna1', '123', '-Anna', 'Anna@x.pl', '<b>Anna</b>', 'Anna_'])('rejects %s as invalid', (name) => {
    expect(firstNameProblem(name)).toBe('invalid')
  })

  it('accepts exactly the maximum length and rejects one more', () => {
    expect(firstNameProblem('a'.repeat(FIRST_NAME_MAX_LENGTH))).toBeNull()
    expect(firstNameProblem('a'.repeat(FIRST_NAME_MAX_LENGTH + 1))).toBe('too_long')
  })
})
