/*======================================
		Remove fields tests
 (based on `Types ordering tests`)
======================================*/

USE `nz_test_closure`;

/*---------------------------------------/
	Remove multiple types
--------------------------------------*/
CALL before_alter_types();
INSERT INTO `type_rem_list`
(`name`)
VALUES
('People'),
('VeryGoodPeople'),
('GoodPeople'),
('Sedan');
CALL remove_types();
