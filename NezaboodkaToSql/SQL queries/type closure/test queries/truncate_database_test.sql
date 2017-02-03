/*======================================
		Truncate database test
======================================*/

CALL before_alter_types();
INSERT INTO `type_rem_list`
(`name`)
SELECT `name`
FROM `nz_test_closure`.`type`;
CALL remove_types();
